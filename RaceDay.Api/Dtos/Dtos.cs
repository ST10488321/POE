using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

// ---------- Auth / Users ----------
public class RegisterDto
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";
    [Required] public UserRole? Role { get; set; }
    /// <summary>Secret invite code, required only when registering as an Organiser.</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class UpdateProfileDto
{
    [StringLength(100)] public string? Name { get; set; }
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [StringLength(100, MinimumLength = 8)] public string? Password { get; set; }
}

public record UserDto(int Id, string Name, string Email, UserRole Role, string? ProfileImageUrl = null);

// ---------- Events ----------
public class EventCreateDto
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, StringLength(1000)] public string Description { get; set; } = "";
    [Required] public DateTime? EventDate { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = "";
    [Required, Range(0.1, 1000)] public decimal? DistanceKm { get; set; }
    [Required] public EventType? Type { get; set; }
    public EventStatus? Status { get; set; }
}

public class EventUpdateDto
{
    [StringLength(150)] public string? Name { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    public DateTime? EventDate { get; set; }
    [StringLength(200)] public string? Location { get; set; }
    [Range(0.1, 1000)] public decimal? DistanceKm { get; set; }
    public EventType? Type { get; set; }
    public EventStatus? Status { get; set; }
}

public record EventDto(int Id, string Name, string Description, DateTime EventDate, string Location,
    decimal DistanceKm, EventType Type, EventStatus Status, int OrganiserId, string? BannerImageUrl,
    IEnumerable<CategoryDto>? Categories = null);

/// <summary>Event plus the number of (non-cancelled) enrolments - used by the Organiser dashboard.</summary>
public record EventSummaryDto(int Id, string Name, string Description, DateTime EventDate, string Location,
    decimal DistanceKm, EventType Type, EventStatus Status, int OrganiserId, string? BannerImageUrl, int EnrolmentCount);

public class ImageUploadDto
{
    /// <summary>The image file (jpg, png or webp, max 5 MB).</summary>
    [Required] public IFormFile File { get; set; } = null!;
}

// ---------- Categories ----------
public class CategoryCreateDto
{
    [Required] public int? EventId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(300)] public string? Description { get; set; }
}

public class CategoryUpdateDto
{
    [StringLength(100)] public string? Name { get; set; }
    [StringLength(300)] public string? Description { get; set; }
}

public record CategoryDto(int Id, int EventId, string Name, string? Description);

// ---------- Enrolments ----------
public class EnrolmentCreateDto
{
    [Required] public int? EventId { get; set; }
    [Required] public int? CategoryId { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}

public class EnrolmentStatusDto
{
    [Required] public EnrolmentStatus? Status { get; set; }
}

public record EnrolmentDto(int Id, int EventId, string EventName, int CategoryId, string CategoryName,
    int ParticipantId, string ParticipantName, EnrolmentStatus Status, string? Notes, DateTime EnrolledAt);

// ---------- Results ----------
public class ResultUpsertDto
{
    [Required] public int? EnrolmentId { get; set; }
    /// <summary>Finish time as hh:mm:ss, e.g. 00:45:30</summary>
    [Required] public TimeSpan? FinishTime { get; set; }
    [Required, Range(1, 100000)] public int? Position { get; set; }
    [StringLength(500)] public string? Comments { get; set; }
}

public class ResultUpdateDto
{
    public TimeSpan? FinishTime { get; set; }
    [Range(1, 100000)] public int? Position { get; set; }
    [StringLength(500)] public string? Comments { get; set; }
}

public record ResultDto(int Id, int EnrolmentId, int EventId, string EventName, DateTime EventDate,
    string CategoryName, string ParticipantName, TimeSpan FinishTime, int Position, int TotalFinishers, string? Comments);

// ---------- Mapping ----------
public static class Mapping
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Name, u.Email, u.Role, u.ProfileImageUrl);
    public static CategoryDto ToDto(this Category c) => new(c.Id, c.EventId, c.Name, c.Description);

    public static EventDto ToDto(this Event e, bool withCategories = false) =>
        new(e.Id, e.Name, e.Description, e.EventDate, e.Location, e.DistanceKm, e.Type, e.Status,
            e.OrganiserId, e.BannerImageUrl, withCategories ? e.Categories.Select(c => c.ToDto()).ToList() : null);

    public static EnrolmentDto ToDto(this Enrolment e) =>
        new(e.Id, e.EventId, e.Event?.Name ?? "", e.CategoryId, e.Category?.Name ?? "",
            e.ParticipantId, e.Participant?.Name ?? "", e.Status, e.Notes, e.EnrolledAt);

    public static ResultDto ToDto(this Result r, int totalFinishers = 0) =>
        new(r.Id, r.EnrolmentId, r.Enrolment?.EventId ?? 0, r.Enrolment?.Event?.Name ?? "",
            r.Enrolment?.Event?.EventDate ?? default, r.Enrolment?.Category?.Name ?? "",
            r.Enrolment?.Participant?.Name ?? "", r.FinishTime, r.Position, totalFinishers, r.Comments);
}
