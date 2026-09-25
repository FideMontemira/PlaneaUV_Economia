using Microsoft.AspNetCore.Mvc;

namespace PlaneaUV_Economia.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}