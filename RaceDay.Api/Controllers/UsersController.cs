using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
[SessionAuthorize]
public class UsersController(RaceDayContext db, IPasswordService hasher, IBlobStorageService blobs,
    ILogger<UsersController> logger) : ApiControllerBase
{
    /// <summary>Get the profile of the logged-in user (either role).</summary>
    /// <remarks>Input: none. Returns: 200 with id, name, email, role, profileImageUrl. 401 not logged in, 404 user no longer exists.</remarks>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        return user is null ? NotFoundMsg("User not found.") : Ok(user.ToDto());
    }

    /// <summary>Update the logged-in user's own profile (either role).</summary>
    /// <remarks>
    /// Input: any of name, email, password (all optional). The role cannot be changed.
    /// Returns: 200 with the updated profile. 400 invalid data, 401 not logged in, 409 email already used.
    /// </remarks>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileDto dto)
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null) return NotFoundMsg("User not found.");

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            if (email != user.Email && await db.Users.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "That email address is already registered." });
            user.Email = email;
        }
        if (!string.IsNullOrWhiteSpace(dto.Name)) user.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Password)) user.PasswordHash = hasher.Hash(dto.Password);

        await db.SaveChangesAsync();
        return Ok(user.ToDto());
    }

    /// <summary>Upload or replace the logged-in user's profile picture (either role). The file is stored in Azure Blob Storage.</summary>
    /// <remarks>
    /// Input: multipart/form-data with one field "file" (jpg, png or webp, max 5 MB).
    /// Returns: 200 with the profile including profileImageUrl. 400 bad file, 401, 503 storage unavailable.
    /// </remarks>
    [HttpPost("me/picture")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(503)]
    public async Task<ActionResult<UserDto>> UploadPicture([FromForm] ImageUploadDto dto)
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null) return NotFoundMsg("User not found.");

        var error = ImageRules.Validate(dto.File, out var ext);
        if (error != null) return BadRequest(new { message = error });

        try
        {
            await using var stream = dto.File.OpenReadStream();
            user.ProfileImageUrl = await blobs.UploadAsync(stream, $"profiles/{user.Id}/{Guid.NewGuid():N}{ext}", dto.File.ContentType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Profile picture upload failed for user {UserId}", user.Id);
            return StatusCode(503, new { message = "The image could not be uploaded right now. Please try again later." });
        }

        await db.SaveChangesAsync();
        return Ok(user.ToDto());
    }
}
