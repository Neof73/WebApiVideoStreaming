using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApiVideoStreaming.Models
{
    public class Video
    {
        public long TotalLength { get; set; }
        public byte[] VideoBytes { get; set; }
    }
}