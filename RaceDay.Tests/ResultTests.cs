using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

public class ResultTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    private async Task<(HttpClient org, HttpClient part, int enrolment)> Setup()
    {
        var org = await Api.LoginAs(factory, "Organiser");
        var part = await Api.LoginAs(factory, "Participant");
        var ev = await Api.CreateEvent(org);
        var cat = await Api.CreateCategory(org, ev);
        var en = await Api.Enrol(part, ev, cat);
        return (org, part, en);
    }

    [Fact]
    public async Task Organiser_CanRecordResult_ParticipantCanViewIt()
    {
        var (org, part, en) = await Setup();
        var post = await org.PostAsJsonAsync("/api/results", new { enrolmentId = en, finishTime = "00:45:30", position = 3 });
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var view = await part.GetAsync($"/api/enrolments/{en}/result");
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        var body = await view.Content.ReadAsStringAsync();
        Assert.Contains("00:45:30", body);
        Assert.Contains("\"position\":3", body);
    }

    [Fact]
    public async Task RecordingTwice_UpdatesInsteadOfDuplicating()
    {
        var (org, part, en) = await Setup();
        await org.PostAsJsonAsync("/api/results", new { enrolmentId = en, finishTime = "00:50:00", position = 5 });
        await org.PostAsJsonAsync("/api/results", new { enrolmentId = en, finishTime = "00:44:00", position = 2 });
        var mine = await part.GetStringAsync("/api/results/me");
        Assert.Contains("00:44:00", mine);
        Assert.DoesNotContain("00:50:00", mine);
    }

    [Fact]
    public async Task OtherParticipant_CannotViewResult_Returns403()
    {
        var (org, _, en) = await Setup();
        await org.PostAsJsonAsync("/api/results", new { enrolmentId = en, finishTime = "00:45:30", position = 1 });
        var stranger = await Api.LoginAs(factory, "Participant");
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/enrolments/{en}/result")).StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotRecordResult_Returns403()
    {
        var (_, _, en) = await Setup();
        var other = await Api.LoginAs(factory, "Organiser");
        var r = await other.PostAsJsonAsync("/api/results", new { enrolmentId = en, finishTime = "00:45:30", position = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task InvalidPosition_Returns400()
    {
        var (org, _, en) = await Setup();
        var r = await org.PostAsJsonAsync("/api/results", new { enrolmentId = en, finishTime = "00:45:30", position = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task ResultNotYetRecorded_Returns404()
    {
        var (_, part, en) = await Setup();
        Assert.Equal(HttpStatusCode.NotFound, (await part.GetAsync($"/api/enrolments/{en}/result")).StatusCode);
    }
}
