using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/categories")]
[SessionAuthorize(Roles.Organiser)]
public class CategoriesController(RaceDayContext db) : ApiControllerBase
{
    /// <summary>Add an age/distance category (e.g. "Under 20", "21km") to one of your events (Organiser only).</summary>
    /// <remarks>Input: eventId, name, description (optional). Returns: 201 new category. 400 invalid, 401, 403 not the event owner/not an Organiser, 404 event not found.</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CategoryDto>> Create(CategoryCreateDto dto)
    {
        var ev = await db.Events.FindAsync(dto.EventId!.Value);
        if (ev is null) return NotFoundMsg("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only add categories to your own events.");

        var cat = new Category { EventId = ev.Id, Name = dto.Name.Trim(), Description = dto.Description?.Trim() };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return Created($"/api/events/{ev.Id}/categories", cat.ToDto());
    }

    /// <summary>Update a category of one of your events (Organiser only).</summary>
    /// <remarks>Input: name and/or description. Returns: 200 updated category. 400, 401, 403, 404.</remarks>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryUpdateDto dto)
    {
        var cat = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.Id == id);
        if (cat is null) return NotFoundMsg("Category not found.");
        if (cat.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only change categories of your own events.");

        if (!string.IsNullOrWhiteSpace(dto.Name)) cat.Name = dto.Name.Trim();
        if (dto.Description != null) cat.Description = dto.Description.Trim();
        await db.SaveChangesAsync();
        return Ok(cat.ToDto());
    }

    /// <summary>Delete a category of one of your events (Organiser only). Fails with 409 if participants are enrolled in it.</summary>
    /// <remarks>Input: category id in the URL. Returns: 204 No Content. 401, 403, 404, 409.</remarks>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.Id == id);
        if (cat is null) return NotFoundMsg("Category not found.");
        if (cat.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only delete categories of your own events.");
        if (await db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return Conflict(new { message = "Participants are enrolled in this category, so it cannot be deleted." });

        db.Categories.Remove(cat);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
