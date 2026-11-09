using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

public class AuthTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Register_Participant_Returns201()
    {
        var c = factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/register",
            new { name = "Sam", email = Api.NewEmail(), password = Api.Password, role = "Participant" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.DoesNotContain("password", (await r.Content.ReadAsStringAsync()).ToLower());
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var c = factory.CreateClient();
        var body = new { name = "Sam", email = Api.NewEmail(), password = Api.Password, role = "Participant" };
        await c.PostAsJsonAsync("/api/auth/register", body);
        var second = await c.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_Returns403()
    {
        var c = factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/register",
            new { name = "Sneaky", email = Api.NewEmail(), password = Api.Password, role = "Organiser" });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithCode_Returns201()
    {
        var c = factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/register",
            new { name = "Org", email = Api.NewEmail(), password = Api.Password, role = "Organiser", organiserCode = Api.OrganiserCode });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var c = factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/register",
            new { name = "Sam", email = Api.NewEmail(), password = "123", role = "Participant" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Password_IsStoredHashed()
    {
        var c = factory.CreateClient();
        var email = Api.NewEmail();
        await c.PostAsJsonAsync("/api/auth/register", new { name = "Sam", email, password = Api.Password, role = "Participant" });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        Assert.NotEqual(Api.Password, user.PasswordHash);
        Assert.DoesNotContain(Api.Password, user.PasswordHash);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200_AndProfileWorks()
    {
        var c = await Api.LoginAs(factory, "Participant");
        var me = await c.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var c = factory.CreateClient();
        var email = Api.NewEmail();
        await c.PostAsJsonAsync("/api/auth/register", new { name = "Sam", email, password = Api.Password, role = "Participant" });
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword9" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutLogin_Returns401()
    {
        var c = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var c = await Api.LoginAs(factory, "Participant");
        await c.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesName()
    {
        var c = await Api.LoginAs(factory, "Participant");
        var r = await c.PutAsJsonAsync("/api/users/me", new { name = "New Name" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("New Name", await r.Content.ReadAsStringAsync());
    }
}
