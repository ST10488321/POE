using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RaceDay.Tests;

public class ImageAndSummaryTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    private static MultipartFormDataContent FileContent(string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Organiser_CanUploadBanner_UrlIsSaved()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var id = await Api.CreateEvent(org);
        var r = await org.PostAsync($"/api/events/{id}/banner", FileContent("banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("fake.blob.core.windows.net", await r.Content.ReadAsStringAsync());

        var get = await org.GetStringAsync($"/api/events/{id}");
        Assert.Contains("fake.blob.core.windows.net", get);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner_Returns403()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var part = await Api.LoginAs(factory, "Participant");
        var id = await Api.CreateEvent(org);
        var r = await part.PostAsync($"/api/events/{id}/banner", FileContent("banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotUploadBanner_Returns403()
    {
        var owner = await Api.LoginAs(factory, "Organiser");
        var other = await Api.LoginAs(factory, "Organiser");
        var id = await Api.CreateEvent(owner);
        var r = await other.PostAsync($"/api/events/{id}/banner", FileContent("banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Banner_WrongFileType_Returns400()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var id = await Api.CreateEvent(org);
        var r = await org.PostAsync($"/api/events/{id}/banner", FileContent("notes.txt", "text/plain"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Banner_WithoutLogin_Returns401()
    {
        var anon = factory.CreateClient();
        var r = await anon.PostAsync("/api/events/1/banner", FileContent("banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task User_CanUploadProfilePicture()
    {
        var part = await Api.LoginAs(factory, "Participant");
        var r = await part.PostAsync("/api/users/me/picture", FileContent("me.jpg", "image/jpeg"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var me = await part.GetStringAsync("/api/users/me");
        Assert.Contains("profiles/", me);
    }

    [Fact]
    public async Task Organiser_MineEndpoint_ReturnsEnrolmentCount()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var part = await Api.LoginAs(factory, "Participant");
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev);
        await Api.Enrol(part, ev, cat);
        var mine = await org.GetStringAsync("/api/events/mine");
        Assert.Contains("\"enrolmentCount\":1", mine);
    }

    [Fact]
    public async Task Participant_CannotCallMineEndpoint_Returns403()
    {
        var part = await Api.LoginAs(factory, "Participant");
        Assert.Equal(HttpStatusCode.Forbidden, (await part.GetAsync("/api/events/mine")).StatusCode);
    }

    [Fact]
    public async Task Results_IncludeCategoryAndTotalFinishers()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var p1 = await Api.LoginAs(factory, "Participant");
        var p2 = await Api.LoginAs(factory, "Participant");
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev);
        var e1 = await Api.Enrol(p1, ev, cat);
        var e2 = await Api.Enrol(p2, ev, cat);
        await org.PostAsJsonAsync("/api/results", new { enrolmentId = e1, finishTime = "00:40:00", position = 1 });
        await org.PostAsJsonAsync("/api/results", new { enrolmentId = e2, finishTime = "00:50:00", position = 2 });

        var mine = await p2.GetStringAsync("/api/results/me");
        Assert.Contains("\"totalFinishers\":2", mine);
        Assert.Contains("\"categoryName\":\"10km\"", mine);
        Assert.Contains("\"position\":2", mine);
    }

    [Fact]
    public async Task Events_CanBeFilteredBySearchAndType()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var part = await Api.LoginAs(factory, "Participant");
        await org.PostAsJsonAsync("/api/events", new
        {
            name = "Unique Zebra Dash", description = "d", eventDate = DateTime.UtcNow.AddDays(10),
            location = "Durban", distanceKm = 5, type = "Walk"
        });
        var hit = await part.GetStringAsync("/api/events?search=zebra&type=Walk&upcoming=true");
        Assert.Contains("Unique Zebra Dash", hit);
        var miss = await part.GetStringAsync("/api/events?search=zebra&type=Cycle");
        Assert.DoesNotContain("Unique Zebra Dash", miss);
    }
}
