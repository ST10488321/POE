using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Security;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

public class AccountController(ApiClient api) : BaseController
{
    [HttpGet]
    public IActionResult Register()
    {
        if (IsLoggedIn) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (vm.Role == "Organiser" && string.IsNullOrWhiteSpace(vm.OrganiserCode))
            ModelState.AddModelError(nameof(vm.OrganiserCode), "Organisers need the organiser code.");
        if (!ModelState.IsValid) return View(vm);

        var r = await api.SendAsync<UserModel>(HttpMethod.Post, "api/auth/register", new
        {
            name = vm.Name,
            email = vm.Email,
            password = vm.Password,
            role = vm.Role,
            organiserCode = vm.Role == "Organiser" ? vm.OrganiserCode : null
        });
        if (!r.Ok)
        {
            ModelState.AddModelError("", r.Error ?? "Registration failed.");
            return View(vm);
        }
        Success("Your account has been created. Please log in.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (IsLoggedIn) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        HttpContext.Session.Clear();                       // new session on every login
        var r = await api.LoginAsync(vm.Email, vm.Password);
        if (!r.Ok || r.Data is null || HttpContext.Session.GetString(SessionKeys.ApiCookie) is null)
        {
            HttpContext.Session.Clear();
            ModelState.AddModelError("", r.Error ?? "We could not log you in. Please try again.");
            return View(vm);
        }

        // Keep who is logged in (and their role) for every later page
        var user = r.Data;
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.Id);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);
        HttpContext.Session.SetString(SessionKeys.Name, user.Name);
        HttpContext.Session.SetString(SessionKeys.Picture, user.ProfileImageUrl ?? "");

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return LocalRedirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, RoleAuthorize]
    public async Task<IActionResult> Logout()
    {
        await api.SendAsync<object>(HttpMethod.Post, "api/auth/logout");   // ends the API session
        HttpContext.Session.Clear();                                         // ends the MVC session
        Success("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpGet, RoleAuthorize]
    public async Task<IActionResult> Profile()
    {
        var r = await api.GetAsync<UserModel>("api/users/me");
        if (!r.Ok || r.Data is null) return Fail(r);
        return View(new ProfileViewModel
        {
            Name = r.Data.Name, Email = r.Data.Email, Role = r.Data.Role, ProfileImageUrl = r.Data.ProfileImageUrl
        });
    }

    [HttpPost, RoleAuthorize]
    public async Task<IActionResult> Profile(ProfileViewModel vm)
    {
        vm.Role = CurrentRole ?? "";
        if (!ModelState.IsValid) return View(vm);

        var r = await api.SendAsync<UserModel>(HttpMethod.Put, "api/users/me", new
        {
            name = vm.Name,
            email = vm.Email,
            password = string.IsNullOrWhiteSpace(vm.NewPassword) ? null : vm.NewPassword
        });
        if (!r.Ok)
        {
            var redirect = AuthRedirect(r.Status);
            if (redirect != null) return redirect;
            ModelState.AddModelError("", r.Error ?? "Your profile could not be saved.");
            return View(vm);
        }

        var user = r.Data!;
        string? pictureWarning = null;
        if (vm.Picture is { Length: > 0 })
        {
            var up = await api.UploadAsync<UserModel>("api/users/me/picture", vm.Picture);
            if (up.Ok && up.Data != null) user = up.Data;
            else pictureWarning = up.Error ?? "The picture could not be uploaded.";
        }

        HttpContext.Session.SetString(SessionKeys.Name, user.Name);
        HttpContext.Session.SetString(SessionKeys.Picture, user.ProfileImageUrl ?? "");

        if (pictureWarning != null) Failure($"Your details were saved, but the picture was not: {pictureWarning}");
        else Success("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }
}
