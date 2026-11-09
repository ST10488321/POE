using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user, read from the server-side session.</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
    protected bool IsOrganiser => HttpContext.Session.GetString(SessionKeys.Role) == Roles.Organiser;

    protected ObjectResult Forbidden(string message) => StatusCode(StatusCodes.Status403Forbidden, new { message });
    protected NotFoundObjectResult NotFoundMsg(string message) => NotFound(new { message });
}
