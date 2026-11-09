using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Security;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[RoleAuthorize("Organiser")]
public class OrganiserController(ApiClient api) : BaseController
{
    // ---------- Dashboard ----------
    public async Task<IActionResult> Dashboard()
    {
        var r = await api.GetAsync<List<EventModel>>("api/events/mine");
        if (!r.Ok) return Fail(r);

        var events = r.Data ?? new List<EventModel>();
        var upcoming = events.Where(e => e.EventDate >= DateTime.UtcNow && e.Status != "Cancelled")
                             .OrderBy(e => e.EventDate).Take(5).ToList();
        return View(new DashboardViewModel
        {
            Events = events,
            Upcoming = upcoming,
            TotalEvents = events.Count,
            TotalEnrolments = events.Sum(e => e.EnrolmentCount),
            UpcomingCount = events.Count(e => e.EventDate >= DateTime.UtcNow && e.Status != "Cancelled")
        });
    }

    // ---------- Events management ----------
    public async Task<IActionResult> Events()
    {
        var r = await api.GetAsync<List<EventModel>>("api/events/mine");
        if (!r.Ok) return Fail(r);
        return View(r.Data ?? new List<EventModel>());
    }

    [HttpGet]
    public IActionResult CreateEvent() =>
        View("EventForm", new EventFormViewModel { EventDate = DateTime.UtcNow.AddDays(14).Date.AddHours(8) });

    [HttpPost]
    public async Task<IActionResult> CreateEvent(EventFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("EventForm", vm);

        var r = await api.SendAsync<EventModel>(HttpMethod.Post, "api/events", EventBody(vm));
        if (!r.Ok || r.Data is null)
        {
            var redirect = AuthRedirect(r.Status);
            if (redirect != null) return redirect;
            ModelState.AddModelError("", r.Error ?? "The event could not be created.");
            return View("EventForm", vm);
        }

        if (vm.Banner is { Length: > 0 })
        {
            var up = await api.UploadAsync<EventModel>($"api/events/{r.Data.Id}/banner", vm.Banner);
            if (!up.Ok) { Failure($"The event was created, but the banner was not uploaded: {up.Error}"); return RedirectToAction(nameof(Events)); }
        }
        Success("Event created.");
        return RedirectToAction(nameof(Events));
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var r = await api.GetAsync<EventModel>($"api/events/{id}");
        if (!r.Ok || r.Data is null) return Fail(r);
        var e = r.Data;
        return View("EventForm", new EventFormViewModel
        {
            Id = e.Id, Name = e.Name, Description = e.Description, EventDate = e.EventDate, Location = e.Location,
            DistanceKm = e.DistanceKm, Type = e.Type, Status = e.Status, BannerImageUrl = e.BannerImageUrl
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel vm)
    {
        vm.Id = id;
        if (!ModelState.IsValid) return View("EventForm", vm);

        var r = await api.SendAsync<EventModel>(HttpMethod.Put, $"api/events/{id}", EventBody(vm));
        if (!r.Ok)
        {
            var redirect = AuthRedirect(r.Status);
            if (redirect != null) return redirect;
            ModelState.AddModelError("", r.Error ?? "The event could not be saved.");
            return View("EventForm", vm);
        }

        if (vm.Banner is { Length: > 0 })
        {
            var up = await api.UploadAsync<EventModel>($"api/events/{id}/banner", vm.Banner);
            if (!up.Ok) { Failure($"The event was saved, but the banner was not uploaded: {up.Error}"); return RedirectToAction(nameof(Events)); }
        }
        Success("Event updated.");
        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var r = await api.SendAsync<object>(HttpMethod.Delete, $"api/events/{id}");
        if (!r.Ok)
        {
            var redirect = AuthRedirect(r.Status);
            if (redirect != null) return redirect;
            Failure(r.Error ?? "The event could not be deleted.");
        }
        else Success("Event deleted.");
        return RedirectToAction(nameof(Events));
    }

    private static object EventBody(EventFormViewModel vm) => new
    {
        name = vm.Name,
        description = vm.Description,
        eventDate = vm.EventDate,
        location = vm.Location,
        distanceKm = vm.DistanceKm,
        type = vm.Type,
        status = vm.Status
    };

    // ---------- Categories ----------
    public async Task<IActionResult> Categories(int? id)
    {
        if (id is null) return await PickEvent("Categories", "Categories - choose an event");

        var r = await api.GetAsync<EventModel>($"api/events/{id}");
        if (!r.Ok || r.Data is null) return Fail(r);
        return View(new CategoriesPageViewModel { Event = r.Data, Form = new CategoryFormViewModel { EventId = r.Data.Id } });
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(CategoryFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            Failure("Please enter a category name (max 100 characters).");
            return RedirectToAction(nameof(Categories), new { id = form.EventId });
        }
        var r = await api.SendAsync<CategoryModel>(HttpMethod.Post, "api/categories",
            new { eventId = form.EventId, name = form.Name, description = form.Description });
        if (!r.Ok)
        {
            var redirect = AuthRedirect(r.Status);
            if (redirect != null) return redirect;
            Failure(r.Error ?? "The category could not be added.");
        }
        else Success("Category added.");
        return RedirectToAction(nameof(Categories), new { id = form.EventId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id, int eventId)
    {
        var r = await api.SendAsync<object>(HttpMethod.Delete, $"api/categories/{id}");
        if (!r.Ok)
        {
            var redirect = AuthRedirect(r.Status);
            if (redirect != null) return redirect;
            Failure(r.Error ?? "The category could not be deleted.");
        }
        else Success("Category deleted.");
        return RedirectToAction(nameof(Categories), new { id = eventId });
    }

    // ---------- Enrolments ----------
    public async Task<IActionResult> Enrolments(int? id)
    {
        if (id is null) return await PickEvent("Enrolments", "Enrolments - choose an event");

        var ev = await api.GetAsync<EventModel>($"api/events/{id}");
        if (!ev.Ok || ev.Data is null) return Fail(ev);
        var en = await api.GetAsync<List<EnrolmentModel>>($"api/enrolments?eventId={id}");
        if (!en.Ok) return Fail(en);
        return View(new EnrolmentsPageViewModel { Event = ev.Data, Enrolments = en.Data ?? new List<EnrolmentModel>() });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateEnrolmentStatus(int id, int eventId, string status)
    {
        var r = await api.SendAsync<EnrolmentModel>(HttpMethod.Put, $"api/enrolments/{id}", new { status });
        if (!r.Ok)
        {
            var redirect = AuthRedirect(r.Status);
            if (redirect != null) return redirect;
            Failure(r.Error ?? "The status could not be changed.");
        }
        else Success("Enrolment status updated.");
        return RedirectToAction(nameof(Enrolments), new { id = eventId });
    }

    // ---------- Results ----------
    public async Task<IActionResult> Results(int? id)
    {
        if (id is null) return await PickEvent("Results", "Results - choose an event");

        var ev = await api.GetAsync<EventModel>($"api/events/{id}");
        if (!ev.Ok || ev.Data is null) return Fail(ev);
        var en = await api.GetAsync<List<EnrolmentModel>>($"api/enrolments?eventId={id}");
        if (!en.Ok) return Fail(en);
        var res = await api.GetAsync<List<ResultModel>>($"api/events/{id}/results");
        if (!res.Ok) return Fail(res);

        var existing = (res.Data ?? new List<ResultModel>()).ToDictionary(x => x.EnrolmentId);
        var rows = (en.Data ?? new List<EnrolmentModel>())
            .Where(e => e.Status != "Cancelled")
            .OrderBy(e => existing.TryGetValue(e.Id, out var x) ? x.Position : int.MaxValue)
            .ThenBy(e => e.ParticipantName)
            .Select(e =>
            {
                existing.TryGetValue(e.Id, out var x);
                return new ResultRow
                {
                    EnrolmentId = e.Id,
                    ParticipantName = e.ParticipantName,
                    CategoryName = e.CategoryName,
                    FinishTime = x is null ? null : UiHelpers.Time(x.FinishTime),
                    Position = x?.Position,
                    Comments = x?.Comments
                };
            }).ToList();

        return View(new ResultsPageViewModel { EventId = ev.Data.Id, EventName = ev.Data.Name, Rows = rows });
    }

    [HttpPost]
    public async Task<IActionResult> SaveResults(ResultsPageViewModel vm)
    {
        var saved = 0;
        var problems = new List<string>();
        var formats = new[] { @"h\:mm\:ss", @"hh\:mm\:ss" };

        foreach (var row in vm.Rows.Where(x => !string.IsNullOrWhiteSpace(x.FinishTime) || x.Position.HasValue))
        {
            if (!TimeSpan.TryParseExact((row.FinishTime ?? "").Trim(), formats, CultureInfo.InvariantCulture, out var time)
                || !row.Position.HasValue || row.Position < 1)
            {
                problems.Add($"{row.ParticipantName}: enter both a finish time (hh:mm:ss) and a position of 1 or higher.");
                continue;
            }

            var r = await api.SendAsync<ResultModel>(HttpMethod.Post, "api/results", new
            {
                enrolmentId = row.EnrolmentId,
                finishTime = time.ToString(@"hh\:mm\:ss"),
                position = row.Position,
                comments = row.Comments
            });
            if (r.Ok) { saved++; continue; }

            var redirect = AuthRedirect(r.Status);
            if (redirect != null) return redirect;
            problems.Add($"{row.ParticipantName}: {r.Error}");
        }

        if (problems.Count > 0) Failure(string.Join(" ", problems));
        if (saved > 0) Success($"{saved} result(s) saved and published to participants.");
        else if (problems.Count == 0) Failure("There were no results to save.");
        return RedirectToAction(nameof(Results), new { id = vm.EventId });
    }

    // ---------- Event picker used by the menu links ----------
    private async Task<IActionResult> PickEvent(string action, string title)
    {
        var r = await api.GetAsync<List<EventModel>>("api/events/mine");
        if (!r.Ok) return Fail(r);
        return View("PickEvent", new EventPickerViewModel { Title = title, TargetAction = action, Events = r.Data ?? new() });
    }
}
