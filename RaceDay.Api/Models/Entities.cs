using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

// Participant is first so an omitted role can never default to Organiser.
public enum UserRole { Participant, Organiser }
public enum EventType { Run, Walk, Cycle }
public enum EventStatus { Open, Closed, Cancelled }
public enum EnrolmentStatus { Pending, Confirmed, Cancelled }

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [Required, MaxLength(150)] public string Email { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";   // never the plain password
    public UserRole Role { get; set; }
    [MaxLength(500)] public string? ProfileImageUrl { get; set; }   // Azure Blob URL
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Event
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = "";
    [Required, MaxLength(1000)] public string Description { get; set; } = "";
    public DateTime EventDate { get; set; }
    [Required, MaxLength(200)] public string Location { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public EventType Type { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Open;
    [MaxLength(500)] public string? BannerImageUrl { get; set; }    // Azure Blob URL
    public int OrganiserId { get; set; }
    public User? Organiser { get; set; }
    public List<Category> Categories { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class Category
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public Event? Event { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(300)] public string? Description { get; set; }
}

public class Enrolment
{
    public int Id { get; set; }
    public int ParticipantId { get; set; }
    public User? Participant { get; set; }
    public int EventId { get; set; }
    public Event? Event { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Confirmed;
    [MaxLength(500)] public string? Notes { get; set; }
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public Result? Result { get; set; }
}

public class Result
{
    public int Id { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }
    public TimeSpan FinishTime { get; set; }
    public int Position { get; set; }
    [MaxLength(500)] public string? Comments { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
