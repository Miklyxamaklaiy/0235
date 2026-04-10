using Microsoft.AspNetCore.Mvc;

namespace PlechPomoshchi.Controllers;

public class AboutController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
