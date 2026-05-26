using Microsoft.AspNetCore.Mvc;

namespace Compound_Sentry.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Login", "Account");
        }
    }
}