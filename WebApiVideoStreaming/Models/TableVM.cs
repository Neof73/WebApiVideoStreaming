using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApiVideoStreaming.Models
{
    public class TableVM
    {
        public TableVM()
        {
            Rows = new List<string>();
            Headers = new List<string>();
        }

        public List<String> Headers { get; set; }
        public List<String> Rows { get; set; }
        public String CurrentVideo { get; set; }

    }
}