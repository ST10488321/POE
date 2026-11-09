using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/enrolments")]
[SessionAuthorize]
public class EnrolmentsController(RaceDayContext db) : ApiControllerBase
{
    /// <summary>List enrolments. Participants see their own; Organisers see all enrolments for their events.</summary>
    /// <remarks>Input: optional query ?eventId=1&amp;participantId=2. Returns: 200 array of enrolments. 401 not logged in.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<IEnumerable<EnrolmentDto>>> GetAll([FromQuery] int? eventId, [FromQuery] int? participantId)
    {
        var q = db.Enrolments.AsNoTracking()
            .Include(e => e.Event).Include(e => e.Category).Include(e => e.Participant)
            .AsQueryable();

        q = IsOrganiser
            ? q.Where(e => e.Event!.OrganiserId == CurrentUserId)
            : q.Where(e => e.ParticipantId == CurrentUserId);

        if (eventId.HasValue) q = q.Where(e => e.EventId == eventId.Value);
        if (participantId.HasValue) q = q.Where(e => e.ParticipantId == participantId.Value);

        var list = await q.OrderByDescending(e => e.EnrolledAt).ToListAsync();
        return Ok(list.Select(e => e.ToDto()));
    }

    /// <summary>Enrol in an event by choosing one of its categories (Participant only).</summary>
    /// <remarks>
    /// Input: eventId, categoryId, notes (optional). The link between participant, event and category is saved.
    /// Returns: 201 new enrolment. 400 invalid / category not part of the event / event not open, 401, 403 not a Participant, 404 event or category not found, 409 already enrolled.
    /// </remarks>
    [HttpPost]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<EnrolmentDto>> Enrol(EnrolmentCreateDto dto)
    {
        var ev = await db.Events.FindAsync(dto.EventId!.Value);
        if (ev is null) return NotFoundMsg("Event not found.");
        var cat = await db.Categories.FindAsync(dto.CategoryId!.Value);
        if (cat is null) return NotFoundMsg("Category not found.");
        if (cat.EventId != ev.Id) return BadRequest(new { message = "That category does not belong to this event." });
        if (ev.Status != EventStatus.Open) return BadRequest(new { message = "This event is not open for enrolment." });
        if (await db.Enrolments.AnyAsync(e => e.ParticipantId == CurrentUserId && e.EventId == ev.Id))
            return Conflict(new { message = "You are already enrolled in this event." });

        var enrolment = new Enrolment
        {
            ParticipantId = CurrentUserId,
            EventId = ev.Id,
            CategoryId = cat.Id,
            Notes = dto.Notes?.Trim(),
            Status = EnrolmentStatus.Confirmed
        };
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();

        await db.Entry(enrolment).Reference(e => e.Event).LoadAsync();
        await db.Entry(enrolment).Reference(e => e.Category).LoadAsync();
        await db.Entry(enrolment).Reference(e => e.Participant).LoadAsync();
        return Created("/api/enrolments", enrolment.ToDto());
    }

    /// <summary>Change the status of an enrolment for one of your events (Organiser only).</summary>
    /// <remarks>Input: status ("Pending", "Confirmed" or "Cancelled"). Returns: 200 updated enrolment. 400, 401, 403, 404.</remarks>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EnrolmentDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<EnrolmentDto>> UpdateStatus(int id, EnrolmentStatusDto dto)
    {
        var en = await db.Enrolments.Include(e => e.Event).Include(e => e.Category).Include(e => e.Participant)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (en is null) return NotFoundMsg("Enrolment not found.");
        if (en.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only manage enrolments for your own events.");

        en.Status = dto.Status!.Value;
        await db.SaveChangesAsync();
        return Ok(en.ToDto());
    }

    /// <summary>Cancel (delete) your own enrolment (Participant only).</summary>
    /// <remarks>Input: enrolment id in the URL. Returns: 204 No Content. 401, 403 not a Participant or not your enrolment, 404 not found.</remarks>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Cancel(int id)
    {
        var en = await db.Enrolments.Include(e => e.Result).FirstOrDefaultAsync(e => e.Id == id);
        if (en is null) return NotFoundMsg("Enrolment not found.");
        if (en.ParticipantId != CurrentUserId) return Forbidden("You can only cancel your own enrolments.");

        if (en.Result != null) db.Results.Remove(en.Result);
        db.Enrolments.Remove(en);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
