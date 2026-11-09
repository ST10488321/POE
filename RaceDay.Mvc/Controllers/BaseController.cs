using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

public abstract class BaseController : Controller
{
    protected bool IsLoggedIn => HttpContext.Session.GetInt32(SessionKeys.UserId) != null;
    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);

    protected void Success(string message) => TempData["Success"] = message;
    protected void Failure(string message) => TempData["Error"] = message;

    /// <summary>Session expired (401) goes to Login, forbidden (403) goes to Access Denied. Otherwise null.</summary>
    protected IActionResult? AuthRedirect(int status)
    {
        if (status == 401)
        {
            HttpContext.Session.Clear();
            Failure("Your session has expired. Please log in again.");
            return RedirectToAction("Login", "Account");
        }
        if (status == 403) return RedirectToAction("AccessDenied", "Account");
        return null;
    }

    /// <summary>For pages that cannot load their data: shows a friendly message page.</summary>
    protected IActionResult Fail(ApiResponse r) =>
        AuthRedirect(r.Status) ?? View("~/Views/Shared/ApiError.cshtml", r.Error ?? "Something went wrong.");
}
