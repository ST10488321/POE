namespace RaceDay.Mvc.Models;

// Shapes returned by the Part 2 API (the MVC app has no database of its own).
public class UserModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? ProfileImageUrl { get; set; }
}

public class CategoryModel
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public class EventModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public string Type { get; set; } = "Run";
    public string Status { get; set; } = "Open";
    public int OrganiserId { get; set; }
    public string? BannerImageUrl { get; set; }
    public int EnrolmentCount { get; set; }
    public List<CategoryModel>? Categories { get; set; }
}

public class EnrolmentModel
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime EnrolledAt { get; set; }
}

public class ResultModel
{
    public int Id { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateTime EventDate { get; set; }
    public string CategoryName { get; set; } = "";
    public string ParticipantName { get; set; } = "";
    public TimeSpan FinishTime { get; set; }
    public int Position { get; set; }
    public int TotalFinishers { get; set; }
    public string? Comments { get; set; }
}

public static class UiHelpers
{
    public static string Ordinal(int n)
    {
        var suffix = (n % 100) is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    public static string TypeIcon(string type) => type switch
    {
        "Run" => "bi-lightning-charge-fill",
        "Walk" => "bi-person-walking",
        "Cycle" => "bi-bicycle",
        _ => "bi-flag-fill"
    };

    public static string Time(TimeSpan t) =>
        $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
}
