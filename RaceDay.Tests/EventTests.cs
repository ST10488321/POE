using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

public class EventTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Organiser_CanCreateEvent_Returns201()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var r = await org.PostAsJsonAsync("/api/events", Api.NewEvent());
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_InvalidData_Returns400()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var r = await org.PostAsJsonAsync("/api/events", new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var id = await Api.CreateEvent(org);
        var r = await org.PutAsJsonAsync($"/api/events/{id}", new { name = "Renamed", type = "Cycle" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Renamed", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_ThenItIsGone()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var id = await Api.CreateEvent(org);
        Assert.Equal(HttpStatusCode.NoContent, (await org.DeleteAsync($"/api/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await org.GetAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotUpdateOrDeleteEvent_Returns403()
    {
        var owner = await Api.LoginAs(factory, "Organiser");
        var other = await Api.LoginAs(factory, "Organiser");
        var id = await Api.CreateEvent(owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/events/{id}", new { name = "Hack" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewEventsAndCategories()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var part = await Api.LoginAs(factory, "Participant");
        var id = await Api.CreateEvent(org);
        await Api.CreateCategory(org, id);
        Assert.Equal(HttpStatusCode.OK, (await part.GetAsync("/api/events")).StatusCode);
        var cats = await part.GetAsync($"/api/events/{id}/categories");
        Assert.Equal(HttpStatusCode.OK, cats.StatusCode);
        Assert.Contains("10km", await cats.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetMissingEvent_Returns404()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        Assert.Equal(HttpStatusCode.NotFound, (await org.GetAsync("/api/events/999999")).StatusCode);
    }
}
