using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

public class EnrolmentTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecorded()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var part = await Api.LoginAs(factory, "Participant");
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev);

        var r = await part.PostAsJsonAsync("/api/enrolments", new { eventId = ev, categoryId = cat });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);

        var mine = await part.GetStringAsync("/api/enrolments");
        Assert.Contains($"\"eventId\":{ev}", mine);
        Assert.Contains($"\"categoryId\":{cat}", mine);

        var organiserView = await org.GetStringAsync($"/api/enrolments?eventId={ev}");
        Assert.Contains($"\"categoryId\":{cat}", organiserView);
    }

    [Fact]
    public async Task EnrollingTwice_Returns409()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var part = await Api.LoginAs(factory, "Participant");
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev);
        await Api.Enrol(part, ev, cat);
        var again = await part.PostAsJsonAsync("/api/enrolments", new { eventId = ev, categoryId = cat });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task CategoryFromAnotherEvent_Returns400()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var part = await Api.LoginAs(factory, "Participant");
        var ev1 = await Api.CreateEvent(org);
        var ev2 = await Api.CreateEvent(org);
        var catOfEv2 = await Api.CreateCategory(org, ev2);
        var r = await part.PostAsJsonAsync("/api/enrolments", new { eventId = ev1, categoryId = catOfEv2 });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_ButNotSomeoneElses()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var p1 = await Api.LoginAs(factory, "Participant");
        var p2 = await Api.LoginAs(factory, "Participant");
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev);
        var en = await Api.Enrol(p1, ev, cat);

        Assert.Equal(HttpStatusCode.Forbidden, (await p2.DeleteAsync($"/api/enrolments/{en}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await p1.DeleteAsync($"/api/enrolments/{en}")).StatusCode);
    }

    [Fact]
    public async Task Participant_DoesNotSeeOtherParticipantsEnrolments()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var p1 = await Api.LoginAs(factory, "Participant");
        var p2 = await Api.LoginAs(factory, "Participant");
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev);
        await Api.Enrol(p1, ev, cat);
        var p2View = await p2.GetStringAsync("/api/enrolments");
        Assert.Equal("[]", p2View);
    }
}
