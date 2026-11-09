using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Security;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}

/// <summary>
/// Session-based authorisation. 401 = not logged in, 403 = logged in with the wrong role.
/// Usage: [SessionAuthorize] (any logged-in user) or [SessionAuthorize(Roles.Organiser)].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class SessionAuthorizeAttribute(params string[] roles) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        if (session.GetInt32(SessionKeys.UserId) is null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "You must log in first." });
            return;
        }
        var role = session.GetString(SessionKeys.Role);
        if (roles.Length > 0 && !roles.Contains(role))
        {
            context.Result = new ObjectResult(new { message = $"This action requires the role: {string.Join(" or ", roles)}." })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
