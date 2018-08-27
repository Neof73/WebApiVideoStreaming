using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using WebApiVideoStreaming.Controllers.Api;
using WebApiVideoStreaming.Models;
using WebApiVideoStreaming.Support;

namespace WebApiVideoStreaming.Controllers
{
    public class VideosController : ApiController
    {
        //private static readonly ObjectCache cache = MemoryCache.Default;
        private const int BlockSize = 1024 * 512;

        // GET api/values
        public HttpResponseMessage Get(string filename)
        {
            var filePath = HttpContext.Current.Server.MapPath("~") + filename;
            if (!File.Exists(filePath))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            var response = Request.CreateResponse();
            response.Headers.AcceptRanges.Add("bytes");

            var streamer = new FileStreamer();
            streamer.FileInfo = new FileInfo(filePath);
            response.Content = new PushStreamContent(streamer.WriteToStream, "video/mp4");

            RangeHeaderValue rangeHeader = Request.Headers.Range;
            if (rangeHeader != null)
            {
                long totalLength = streamer.FileInfo.Length;
                var range = rangeHeader.Ranges.First();
                streamer.Start = range.From ?? 0;
                streamer.End = range.To ?? totalLength - 1;

                response.Content.Headers.ContentLength = streamer.End - streamer.Start + 1;
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(streamer.Start, streamer.End,
                    totalLength);
                response.StatusCode = HttpStatusCode.PartialContent;
            }
            else
            {
                response.StatusCode = HttpStatusCode.OK;
            }
            return response;
        }

        public HttpResponseMessage Get(string filename, string ext)
        {
            var video = new VideoStream(filename);

            var response = Request.CreateResponse();
            response.Content = new PushStreamContent((Action<Stream, HttpContent, TransportContext>) video.WriteToStream, new MediaTypeHeaderValue("video/" + ext));

            return response;
        }

        public async Task<HttpResponseMessage> GetStream(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                return new HttpResponseMessage(){StatusCode = HttpStatusCode.BadRequest};
            }

            var start = 0L;
            var blockSize = 0L; // BlockSize;
            var chuncked = true;
            if (Request.Headers.Range != null)
            {
                RangeHeaderValue currentRangeHeader = Request.Headers.Range;
                var range = currentRangeHeader.Ranges.First();
                start = range.From ?? 0;
                blockSize = BlockSize;
                chuncked = false;
            } 

            var video = await SqlStreamController.GetFileStream(name, blockSize, start);
            return video == null
                ? new HttpResponseMessage() { StatusCode = HttpStatusCode.BadRequest }
                : GetResponse(video, chuncked);
        }

        private HttpResponseMessage GetResponse(Video video, bool chuncked)
        {
            var response = Request.CreateResponse();
            response.Headers.AcceptRanges.Add("bytes");
            var totalLength = video.TotalLength;
            const string videoType = "video/mp4";
            var start = video.Start;
            var ends = (video.Start + video.VideoBytes.Length - 1 > totalLength ? totalLength - 1 : video.Start + video.VideoBytes.Length - 1);

            response.Content = new ByteArrayContent(video.VideoBytes);
            response.Content.Headers.ContentLength = ends - start + 1;
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, ends == -1 ? 0 : ends, totalLength);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(videoType);
            response.Headers.TransferEncodingChunked = chuncked;
            response.StatusCode = HttpStatusCode.PartialContent;
            return response;
        }

        [HttpPost]
        public async Task<HttpResponseMessage> MediaUpload()
        {
            // Check if the request contains multipart/form-data.  
            if (!Request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            var provider = await Request.Content.ReadAsMultipartAsync<InMemoryMultipartFormDataStreamProvider>(new InMemoryMultipartFormDataStreamProvider());
            //access form data  
            NameValueCollection formData = provider.FormData;
            //access files  
            IList<HttpContent> files = provider.Files;

            HttpContent file1 = files[0];
            Stream file = await file1.ReadAsStreamAsync();
            var name = file1.Headers.ContentDisposition.FileName.Replace("\"", "");

            await AddSqlStreamController.StreamBLOBToServer(name, file);

            var response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri("/VideoStream", UriKind.Relative);
            return response;
        }
    }
}
