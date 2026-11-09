using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

public class RoleEnforcementTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var part = await Api.LoginAs(factory, "Participant");
        Assert.Equal(HttpStatusCode.Forbidden, (await part.PostAsJsonAsync("/api/events", Api.NewEvent())).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateCategory_Returns403()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var part = await Api.LoginAs(factory, "Participant");
        var id = await Api.CreateEvent(org);
        var r = await part.PostAsJsonAsync("/api/categories", new { eventId = id, name = "Hack" });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var id = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, id);
        var r = await org.PostAsJsonAsync("/api/enrolments", new { eventId = id, categoryId = cat });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotRecordResult_Returns403()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var part = await Api.LoginAs(factory, "Participant");
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev);
        var en = await Api.Enrol(part, ev, cat);
        var r = await part.PostAsJsonAsync("/api/results", new { enrolmentId = en, finishTime = "00:40:00", position = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CannotCreateEvent_Returns401()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/events", Api.NewEvent())).StatusCode);
    }
}
