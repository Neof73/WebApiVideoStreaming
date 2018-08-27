using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using WebApiVideoStreaming.Controllers.Api;
using WebApiVideoStreaming.Models;
using System.Net.Http.Headers;

namespace WebApiVideoStreaming.Support
{
    public class VideoStream
    {
        private readonly string _filename;

        public VideoStream(string filename)
        {
            _filename = filename;
        }

        public async void WriteToStream(Stream outputStream, HttpContent content, TransportContext context)
        {
            try
            {
                var blocksize = 65536;
                if (content.Headers.ContentRange == null)
                {
                    content.Headers.ContentRange = new ContentRangeHeaderValue(blocksize);
                }
                Video video = await SqlStreamController.GetFileStream(_filename, blocksize, content.Headers.ContentRange.From??0);

                var length = video.TotalLength;
                var bytesRead = 1;

                while (length > 0 && bytesRead > 0)
                {
                    bytesRead = video.VideoBytes.Length;
                    await outputStream.WriteAsync(video.VideoBytes, 0, bytesRead);
                    length -= bytesRead;
                }
            }
            catch (HttpException ex)
            {
                return;
            }
            finally
            {
                outputStream.Close();
            }
        }
    }
}