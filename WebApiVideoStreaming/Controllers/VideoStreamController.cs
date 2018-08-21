using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using WebApiVideoStreaming.Controllers.Api;
using WebApiVideoStreaming.Models;

namespace WebApiVideoStreaming.Controllers
{
    public class VideoStreamController : Controller
    {
        // GET: VideoStream

        public async Task<ActionResult> Index()
        {
            ViewBag.Title = "Video Streaming";
            List<string> list = await SqlStreamController.GetVideoList();
            TableVM model = new TableVM
            {
                Rows = list,
                Headers = new List<string> { "Nombre", "Acción" },
                CurrentVideo = ""
            };
            return View(model);
        }

        public async Task<ActionResult> GetStream(string name)
        {
            ViewBag.Title = "Video Streaming";
            List<string> list = await SqlStreamController.GetVideoList();
            TableVM model = new TableVM
            {
                Rows = list,
                Headers = new List<string> { "Nombre", "Acción" },
                CurrentVideo = name
            };
            
            return View("index", model);

        }
    }
}