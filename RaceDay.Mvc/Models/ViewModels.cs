using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.Models;

public class RegisterViewModel
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare("Password", ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")] public string ConfirmPassword { get; set; } = "";
    [Required] public string Role { get; set; } = "Participant";
    [Display(Name = "Organiser code")] public string? OrganiserCode { get; set; }
}

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public class ProfileViewModel
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password), Display(Name = "New password (optional)")] public string? NewPassword { get; set; }
    public string Role { get; set; } = "";
    public string? ProfileImageUrl { get; set; }
    [Display(Name = "Profile picture")] public IFormFile? Picture { get; set; }
}

public class EventFormViewModel
{
    public int? Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, StringLength(1000)] public string Description { get; set; } = "";
    [Required, DataType(DataType.DateTime), Display(Name = "Date and time")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime EventDate { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = "";
    [Required, Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")] public decimal? DistanceKm { get; set; }
    [Required, Display(Name = "Event type")] public string Type { get; set; } = "Run";
    [Required] public string Status { get; set; } = "Open";
    [Display(Name = "Banner image")] public IFormFile? Banner { get; set; }
    public string? BannerImageUrl { get; set; }
}

public class CategoryFormViewModel
{
    public int EventId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(300)] public string? Description { get; set; }
}

public class EnrolViewModel
{
    public int EventId { get; set; }
    [Required(ErrorMessage = "Please choose a category.")] public int? CategoryId { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}

public class EventDetailsViewModel
{
    public EventModel Event { get; set; } = new();
    public EnrolmentModel? Existing { get; set; }
    public EnrolViewModel Form { get; set; } = new();
}

public class DashboardViewModel
{
    public List<EventModel> Events { get; set; } = new();
    public List<EventModel> Upcoming { get; set; } = new();
    public int TotalEvents { get; set; }
    public int TotalEnrolments { get; set; }
    public int UpcomingCount { get; set; }
}

public class EventPickerViewModel
{
    public string Title { get; set; } = "";
    public string TargetAction { get; set; } = "";
    public List<EventModel> Events { get; set; } = new();
}

public class CategoriesPageViewModel
{
    public EventModel Event { get; set; } = new();
    public CategoryFormViewModel Form { get; set; } = new();
}

public class EnrolmentsPageViewModel
{
    public EventModel Event { get; set; } = new();
    public List<EnrolmentModel> Enrolments { get; set; } = new();
}

public class ResultRow
{
    public int EnrolmentId { get; set; }
    public string ParticipantName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    [RegularExpression(@"^\d{1,2}:[0-5]\d:[0-5]\d$", ErrorMessage = "Use hh:mm:ss")]
    public string? FinishTime { get; set; }
    public int? Position { get; set; }
    public string? Comments { get; set; }
}

public class ResultsPageViewModel
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public List<ResultRow> Rows { get; set; } = new();
}

public class ParticipantHomeViewModel
{
    public List<EventModel> Events { get; set; } = new();
    public string? Search { get; set; }
    public string? Type { get; set; }
    public DateTime? Date { get; set; }
}
