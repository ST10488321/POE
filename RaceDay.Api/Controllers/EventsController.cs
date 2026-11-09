using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
[SessionAuthorize]
public class EventsController(RaceDayContext db, IBlobStorageService blobs, ILogger<EventsController> logger) : ApiControllerBase
{
    /// <summary>List events (both roles), with optional filters.</summary>
    /// <remarks>Input: optional query string ?status=Open&amp;type=Run&amp;date=2026-11-01&amp;search=park&amp;upcoming=true. Returns: 200 array of events. 401 not logged in.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<IEnumerable<EventDto>>> GetAll(
        [FromQuery] EventStatus? status, [FromQuery] EventType? type, [FromQuery] DateTime? date,
        [FromQuery] string? search, [FromQuery] bool? upcoming)
    {
        var q = db.Events.AsNoTracking().AsQueryable();
        if (status.HasValue) q = q.Where(e => e.Status == status.Value);
        if (type.HasValue) q = q.Where(e => e.Type == type.Value);
        if (date.HasValue) q = q.Where(e => e.EventDate.Date == date.Value.Date);
        if (upcoming == true) q = q.Where(e => e.EventDate >= DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(e => e.Name.ToLower().Contains(s) || e.Location.ToLower().Contains(s));
        }
        var list = await q.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(list.Select(e => e.ToDto()));
    }

    /// <summary>List the events created by the logged-in Organiser, with enrolment counts (Organiser only).</summary>
    /// <remarks>Input: none. Returns: 200 array of events with enrolmentCount. 401, 403 not an Organiser.</remarks>
    [HttpGet("mine")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EventSummaryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<IEnumerable<EventSummaryDto>>> GetMine()
    {
        var list = await db.Events.AsNoTracking()
            .Where(e => e.OrganiserId == CurrentUserId)
            .OrderBy(e => e.EventDate)
            .Select(e => new EventSummaryDto(e.Id, e.Name, e.Description, e.EventDate, e.Location, e.DistanceKm,
                e.Type, e.Status, e.OrganiserId, e.BannerImageUrl,
                e.Enrolments.Count(x => x.Status != EnrolmentStatus.Cancelled)))
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Get one event together with its categories (both roles).</summary>
    /// <remarks>Input: event id in the URL. Returns: 200 event + categories + banner URL. 401 not logged in, 404 not found.</remarks>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<EventDto>> GetById(int id)
    {
        var ev = await db.Events.AsNoTracking().Include(e => e.Categories).FirstOrDefaultAsync(e => e.Id == id);
        return ev is null ? NotFoundMsg("Event not found.") : Ok(ev.ToDto(withCategories: true));
    }

    /// <summary>Create a new event (Organiser only).</summary>
    /// <remarks>
    /// Input: name, description, eventDate (future), location, distanceKm, type ("Run", "Walk" or "Cycle"), status (optional, default Open).
    /// Returns: 201 with the new event. 400 invalid data, 401 not logged in, 403 not an Organiser.
    /// </remarks>
    [HttpPost]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<EventDto>> Create(EventCreateDto dto)
    {
        if (dto.EventDate!.Value <= DateTime.UtcNow)
            return BadRequest(new { message = "The event date must be in the future." });

        var ev = new Event
        {
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            EventDate = dto.EventDate.Value,
            Location = dto.Location.Trim(),
            DistanceKm = dto.DistanceKm!.Value,
            Type = dto.Type!.Value,
            Status = dto.Status ?? EventStatus.Open,
            OrganiserId = CurrentUserId
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.Id }, ev.ToDto());
    }

    /// <summary>Update an event (only the Organiser who created it).</summary>
    /// <remarks>Input: any event fields (all optional). Returns: 200 updated event. 400 invalid, 401 not logged in, 403 not the owner/not an Organiser, 404 not found.</remarks>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<EventDto>> Update(int id, EventUpdateDto dto)
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null) return NotFoundMsg("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only change your own events.");

        if (!string.IsNullOrWhiteSpace(dto.Name)) ev.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Description)) ev.Description = dto.Description.Trim();
        if (dto.EventDate.HasValue) ev.EventDate = dto.EventDate.Value;
        if (!string.IsNullOrWhiteSpace(dto.Location)) ev.Location = dto.Location.Trim();
        if (dto.DistanceKm.HasValue) ev.DistanceKm = dto.DistanceKm.Value;
        if (dto.Type.HasValue) ev.Type = dto.Type.Value;
        if (dto.Status.HasValue) ev.Status = dto.Status.Value;

        await db.SaveChangesAsync();
        return Ok(ev.ToDto());
    }

    /// <summary>Upload or replace the banner image of one of your events (Organiser only). The file is stored in Azure Blob Storage.</summary>
    /// <remarks>
    /// Input: multipart/form-data with one field "file" (jpg, png or webp, max 5 MB).
    /// Returns: 200 event with the new bannerImageUrl. 400 bad file, 401, 403 not the owner/not an Organiser, 404 event not found, 503 storage unavailable.
    /// </remarks>
    [HttpPost("{id:int}/banner")]
    [SessionAuthorize(Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EventDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(503)]
    public async Task<ActionResult<EventDto>> UploadBanner(int id, [FromForm] ImageUploadDto dto)
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null) return NotFoundMsg("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only change the banner of your own events.");

        var error = ImageRules.Validate(dto.File, out var ext);
        if (error != null) return BadRequest(new { message = error });

        try
        {
            await using var stream = dto.File.OpenReadStream();
            ev.BannerImageUrl = await blobs.UploadAsync(stream, $"events/{ev.Id}/{Guid.NewGuid():N}{ext}", dto.File.ContentType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Banner upload failed for event {EventId}", id);
            return StatusCode(503, new { message = "The image could not be uploaded right now. Please try again later." });
        }

        await db.SaveChangesAsync();
        return Ok(ev.ToDto());
    }

    /// <summary>Delete an event and its categories, enrolments and results (only the Organiser who created it).</summary>
    /// <remarks>Input: event id in the URL. Returns: 204 No Content. 401 not logged in, 403 not the owner/not an Organiser, 404 not found.</remarks>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await db.Events
            .Include(e => e.Categories)
            .Include(e => e.Enrolments).ThenInclude(en => en.Result)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev is null) return NotFoundMsg("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only delete your own events.");

        foreach (var en in ev.Enrolments.Where(en => en.Result != null))
            db.Results.Remove(en.Result!);
        db.Enrolments.RemoveRange(ev.Enrolments);
        db.Categories.RemoveRange(ev.Categories);
        db.Events.Remove(ev);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>List the categories of one event (both roles).</summary>
    /// <remarks>Input: event id in the URL. Returns: 200 array of categories. 401 not logged in, 404 event not found.</remarks>
    [HttpGet("{id:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories(int id)
    {
        if (!await db.Events.AnyAsync(e => e.Id == id)) return NotFoundMsg("Event not found.");
        var cats = await db.Categories.AsNoTracking().Where(c => c.EventId == id).ToListAsync();
        return Ok(cats.Select(c => c.ToDto()));
    }
}
