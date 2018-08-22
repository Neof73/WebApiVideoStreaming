using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Http;
using WebApiVideoStreaming.Controllers.Api;
using WebApiVideoStreaming.Models;

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

        public async Task<HttpResponseMessage> GetStream(string name)
        {


            if (String.IsNullOrEmpty(name))
            {
                return new HttpResponseMessage(){StatusCode = HttpStatusCode.BadRequest};
            }

            
            //if (cache[name] == null)
            //{
            //    videobytes = await SqlStreamController.GetBinaryValue(name);
            //    cache.Set(name, videobytes, DateTime.Now.AddMinutes(30));
            //} else
            //{
            //    videobytes = cache[name] as byte[];
            //}

            //if (videobytes == null)
            //{
            //    return response;
            //}
        
            RangeHeaderValue currentRangeHeader = Request.Headers.Range;
            var range = currentRangeHeader.Ranges.First();
            var start = range.From ?? 0;

            var video = await SqlStreamController.GetFileStream(name, BlockSize, start);
            return video == null 
                ? new HttpResponseMessage() { StatusCode = HttpStatusCode.BadRequest } 
                : GetResponse(video);
        }

        private HttpResponseMessage GetResponse(Video video)
        {
            var response = Request.CreateResponse();
            response.Headers.AcceptRanges.Add("bytes");
            var totalLength = video.TotalLength;
            const string videoType = "video/mp4";
            var currentRangeHeader = Request.Headers.Range;
            var range = currentRangeHeader.Ranges.First();
            var start = range.From ?? 0;
            var ends = range.To ?? (start + BlockSize - 1 > totalLength ? totalLength - 1 : start + BlockSize - 1);
            response.Content = new ByteArrayContent(video.VideoBytes);
            response.Content.Headers.ContentLength = ends - start + 1;
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, ends, totalLength);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(videoType);
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

        class FileStreamer
        {
            public FileInfo FileInfo { get; set; }
            public long Start { get; set; }
            public long End { get; set; }

            public async Task WriteToStream(Stream outputStream, HttpContent content, TransportContext context)
            {
                try
                {
                    var buffer = new byte[65536];
                    using (var video = FileInfo.OpenRead())
                    {
                        if (End == -1)
                        {
                            End = video.Length;
                        }
                        var position = Start;
                        var bytesLeft = End - Start + 1;
                        video.Position = Start;
                        while (position <= End)
                        {
                            // what should i do here?
                            var bytesRead = video.Read(buffer, 0, (int)Math.Min(bytesLeft, buffer.Length));
                            await outputStream.WriteAsync(buffer, 0, bytesRead);
                            position += bytesRead;
                            bytesLeft = End - position + 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // fail silently
                }
                finally
                {
                    outputStream.Close();
                }
            }
        }
    }
}
