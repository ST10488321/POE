using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Security;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

[RoleAuthorize("Participant")]
public class ParticipantController(ApiClient api) : BaseController
{
    // ---------- Home: upcoming events with filters ----------
    public async Task<IActionResult> Home(string? search, string? type, DateTime? date)
    {
        var query = new List<string> { "upcoming=true", "status=Open" };
        if (!string.IsNullOrWhiteSpace(search)) query.Add("search=" + Uri.EscapeDataString(search.Trim()));
        if (!string.IsNullOrWhiteSpace(type)) query.Add("type=" + Uri.EscapeDataString(type));
        if (date.HasValue) query.Add("date=" + date.Value.ToString("yyyy-MM-dd"));

        var r = await api.GetAsync<List<EventModel>>("api/events?" + string.Join("&", query));
        if (!r.Ok) return Fail(r);
        return View(new ParticipantHomeViewModel { Events = r.Data ?? new(), Search = search, Type = type, Date = date });
    }

    // ---------- Event detail + enrol ----------
    public async Task<IActionResult> Details(int id)
    {
        var ev = await api.GetAsync<EventModel>($"api/events/{id}");
        if (!ev.Ok || ev.Data is null) return Fail(ev);

        var mine = await api.GetAsync<List<EnrolmentModel>>("api/enrolments");
        var existing = mine.Ok ? mine.Data?.FirstOrDefault(e => e.EventId == id) : null;

        return View(new EventDetailsViewModel { Event = ev.Data, Existing = existing, Form = new EnrolViewModel { EventId = id } });
    }

    [HttpPost]
    public async Task<IActionResult> Enrol(EnrolViewModel form)
    {
        if (!ModelState.IsValid)
        {
            Failure("Please choose a category before entering the event.");
            return RedirectToAction(nameof(Details), new { id = form.EventId });
        }

        var r = await api.SendAsync<EnrolmentModel>(HttpMethod.Post, "api/enrolments",
            new { eventId = form.EventId, categoryId = form.CategoryId, notes = form.Notes });
        if (!r.Ok)
        {
            var redirect = AuthRedirect(r.Status);
            if (redirect != null) return redirect;
            Failure(r.Error ?? "You could not be enrolled.");
            return RedirectToAction(nameof(Details), new { id = form.EventId });
        }
        Success("You are enrolled! Good luck.");
        return RedirectToAction(nameof(MyEvents));
    }

    // ---------- My events ----------
    public async Task<IActionResult> MyEvents()
    {
        var r = await api.GetAsync<List<EnrolmentModel>>("api/enrolments");
        if (!r.Ok) return Fail(r);
        return View(r.Data ?? new List<EnrolmentModel>());
    }

    [HttpPost]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var r = await api.SendAsync<object>(HttpMethod.Delete, $"api/enrolments/{id}");
        if (!r.Ok)
        {
            var redirect = AuthRedirect(r.Status);
            if (redirect != null) return redirect;
            Failure(r.Error ?? "The enrolment could not be cancelled.");
        }
        else Success("Your enrolment has been cancelled.");
        return RedirectToAction(nameof(MyEvents));
    }

    // ---------- My results ----------
    public async Task<IActionResult> MyResults()
    {
        var r = await api.GetAsync<List<ResultModel>>("api/results/me");
        if (!r.Ok) return Fail(r);
        return View(r.Data ?? new List<ResultModel>());
    }
}
