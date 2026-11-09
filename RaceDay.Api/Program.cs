using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Database (EF Core, Code-First, SQL Server)
builder.Services.AddDbContext<RaceDayContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Controllers (enums are sent/received as text, e.g. "Organiser")
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// 3. Server-side session (stores UserId + Role after login)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".RaceDay.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();   // Azure Blob Storage (Part 3)

// 4. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "REST API for managing running, walking and cycling events. " +
                      "Log in via POST /api/auth/login first - the session cookie is then sent automatically by Swagger UI."
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "RaceDay.Api.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml, includeControllerXmlComments: true);
});

var app = builder.Build();

// Apply migrations automatically (skipped for the test environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<RaceDayContext>().Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1"));

app.UseSession();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

public partial class Program { } // needed so the test project can start the API
