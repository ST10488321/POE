using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Mvc.Controllers;

public class HomeController : BaseController
{
    public IActionResult Index()
    {
        if (!IsLoggedIn) return View("Landing");
        return CurrentRole == "Organiser"
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Home", "Participant");
    }

    [HttpGet, HttpPost, IgnoreAntiforgeryToken]
    public IActionResult Error(int? code)
    {
        ViewBag.Code = code;
        ViewBag.Message = code == 404
            ? "We could not find that page."
            : "Something went wrong on our side. Please try again.";
        return View();
    }
}
