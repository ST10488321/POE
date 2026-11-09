using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaceDay.Mvc.Models;

namespace RaceDay.Mvc.Services;

public class ApiResponse
{
    public bool Ok { get; set; }
    public int Status { get; set; }
    public string? Error { get; set; }
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }
}

/// <summary>
/// Talks to the Part 2 API. Every request carries the API session cookie that was
/// stored in this user's MVC session at login. The MVC app never touches a database or Azure directly.
/// </summary>
public class ApiClient(HttpClient http, IHttpContextAccessor accessor)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private ISession Session => accessor.HttpContext!.Session;

    public Task<ApiResponse<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url);

    public Task<ApiResponse<T>> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body != null)
            req.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        return SendCoreAsync<T>(req, captureCookie: false);
    }

    /// <summary>Sends a file to the API (multipart). The API stores it in Azure Blob Storage.</summary>
    public async Task<ApiResponse<T>> UploadAsync<T>(string url, IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var part = new StreamContent(stream);
        part.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(part, "file", Path.GetFileName(file.FileName));
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        return await SendCoreAsync<T>(req, captureCookie: false);
    }

    /// <summary>Logs in through the API and remembers the API session cookie for this user.</summary>
    public Task<ApiResponse<UserModel>> LoginAsync(string email, string password)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { email, password }, Json), Encoding.UTF8, "application/json")
        };
        return SendCoreAsync<UserModel>(req, captureCookie: true);
    }

    private async Task<ApiResponse<T>> SendCoreAsync<T>(HttpRequestMessage req, bool captureCookie)
    {
        var cookie = Session.GetString(SessionKeys.ApiCookie);
        if (!string.IsNullOrEmpty(cookie)) req.Headers.TryAddWithoutValidation("Cookie", cookie);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ApiResponse<T> { Status = 503, Error = "The RaceDay service is not reachable right now. Please try again shortly." };
        }

        using (resp)
        {
            if (captureCookie && resp.Headers.TryGetValues("Set-Cookie", out var values))
            {
                var sessionCookie = values.FirstOrDefault(v => v.StartsWith(".RaceDay.Session=", StringComparison.Ordinal));
                if (sessionCookie != null)
                    Session.SetString(SessionKeys.ApiCookie, sessionCookie.Split(';')[0]);
            }

            var body = await resp.Content.ReadAsStringAsync();
            var result = new ApiResponse<T> { Status = (int)resp.StatusCode, Ok = resp.IsSuccessStatusCode };
            if (resp.IsSuccessStatusCode)
            {
                if (typeof(T) != typeof(object) && !string.IsNullOrWhiteSpace(body))
                {
                    try { result.Data = JsonSerializer.Deserialize<T>(body, Json); }
                    catch (JsonException) { result.Ok = false; result.Error = "The service returned an unexpected response."; }
                }
            }
            else
            {
                result.Error = FriendlyError(result.Status, body);
            }
            return result;
        }
    }

    /// <summary>Turns an API error body into a short, readable message (never raw JSON or stack traces).</summary>
    private static string FriendlyError(int status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                    return m.GetString()!;
                if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Object)
                {
                    var messages = errs.EnumerateObject()
                        .SelectMany(p => p.Value.EnumerateArray().Select(x => x.GetString()))
                        .Where(s => !string.IsNullOrEmpty(s));
                    var text = string.Join(" ", messages);
                    if (text.Length > 0) return text;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { }

        return status switch
        {
            400 => "Some of the details you entered are not valid.",
            401 => "Please log in to continue.",
            403 => "You are not allowed to do that.",
            404 => "That item could not be found.",
            409 => "That conflicts with existing information.",
            _ => "Something went wrong. Please try again."
        };
    }
}
