using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>Starts the real API in memory, with an in-memory database instead of SQL Server.</summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RaceDayContext>>();
            var dbName = "RaceDayTests-" + Guid.NewGuid();
            services.AddDbContext<RaceDayContext>(o => o.UseInMemoryDatabase(dbName));

            // Never talk to real Azure in tests
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService, FakeBlobStorage>();
        });
    }
}

public class FakeBlobStorage : IBlobStorageService
{
    public Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct = default)
        => Task.FromResult($"https://fake.blob.core.windows.net/raceday-images/{blobName}");
}

public static class Api
{
    public const string OrganiserCode = "RACEDAY-ORG-2026";
    public const string Password = "Password1!";

    public static string NewEmail() => $"{Guid.NewGuid():N}@test.com";

    /// <summary>Registers a fresh user, logs in, and returns a client that holds the session cookie.</summary>
    public static async Task<HttpClient> LoginAs(RaceDayFactory f, string role)
    {
        var client = f.CreateClient();
        var email = NewEmail();
        var reg = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Test User", email, password = Password, role,
            organiserCode = role == "Organiser" ? OrganiserCode : null
        });
        reg.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        return client;
    }

    public static async Task<int> IdOf(HttpResponseMessage r)
    {
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetInt32();
    }

    public static object NewEvent(string name = "Test Race") => new
    {
        name, description = "A fun race", eventDate = DateTime.UtcNow.AddDays(30),
        location = "Cape Town", distanceKm = 10, type = "Run"
    };

    public static async Task<int> CreateEvent(HttpClient organiser)
    {
        var r = await organiser.PostAsJsonAsync("/api/events", NewEvent());
        r.EnsureSuccessStatusCode();
        return await IdOf(r);
    }

    public static async Task<int> CreateCategory(HttpClient organiser, int eventId)
    {
        var r = await organiser.PostAsJsonAsync("/api/categories", new { eventId, name = "10km", description = "Open" });
        r.EnsureSuccessStatusCode();
        return await IdOf(r);
    }

    public static async Task<int> Enrol(HttpClient participant, int eventId, int categoryId)
    {
        var r = await participant.PostAsJsonAsync("/api/enrolments", new { eventId, categoryId, notes = "Ready" });
        r.EnsureSuccessStatusCode();
        return await IdOf(r);
    }
}
