using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Mvc.Security;

/// <summary>
/// Protects MVC pages using the session. Not logged in = redirect to Login.
/// Wrong role = redirect to the Access Denied page. No roles given = any logged-in user.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RoleAuthorizeAttribute(params string[] roles) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        if (session.GetInt32(SessionKeys.UserId) is null)
        {
            var returnUrl = context.HttpContext.Request.Method == "GET"
                ? context.HttpContext.Request.Path + context.HttpContext.Request.QueryString
                : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }
        var role = session.GetString(SessionKeys.Role);
        if (roles.Length > 0 && !roles.Contains(role))
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
    }
}
