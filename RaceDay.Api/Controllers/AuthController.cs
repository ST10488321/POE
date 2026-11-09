using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(RaceDayContext db, IPasswordService hasher, IConfiguration config) : ControllerBase
{
    /// <summary>Register a new account as a Participant or an Organiser.</summary>
    /// <remarks>
    /// Input: name, email, password (min 8 chars), role ("Participant" or "Organiser").
    /// Organisers must also send the secret organiserCode so nobody can simply pick the Organiser role.
    /// Returns: 201 with the new user (the password is never returned). 400 invalid data, 403 wrong organiser code, 409 email already used.
    /// </remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<UserDto>> Register(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "That email address is already registered." });

        var role = dto.Role!.Value;
        if (role == UserRole.Organiser)
        {
            var code = config["Registration:OrganiserCode"];
            if (string.IsNullOrEmpty(code) || dto.OrganiserCode != code)
                return StatusCode(403, new { message = "A valid organiser code is required to register as an Organiser." });
        }

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(dto.Password),
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Created("/api/users/me", user.ToDto());
    }

    /// <summary>Log in with email and password and start a session.</summary>
    /// <remarks>
    /// Input: email, password. On success the server stores the user id and role in the session
    /// and returns a session cookie, which is sent automatically on every later request.
    /// Returns: 200 with the user. 401 for wrong credentials.
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<UserDto>> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !hasher.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.Id);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.ToString());
        return Ok(user.ToDto());
    }

    /// <summary>Log out and destroy the session.</summary>
    /// <remarks>Input: none. Returns: 204 No Content.</remarks>
    [HttpPost("logout")]
    [ProducesResponseType(204)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }
}
