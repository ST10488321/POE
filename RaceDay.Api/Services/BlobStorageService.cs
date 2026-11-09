using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

public interface IBlobStorageService
{
    /// <summary>Uploads a file to Azure Blob Storage and returns its public URL.</summary>
    Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct = default);
}

public class BlobStorageService(IConfiguration config) : IBlobStorageService
{
    public async Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct = default)
    {
        var connectionString = config["AzureBlob:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Azure Blob Storage is not configured (AzureBlob:ConnectionString).");

        var containerName = config["AzureBlob:ContainerName"] ?? "raceday-images";
        var container = new BlobContainerClient(connectionString, containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);

        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
        return blob.Uri.ToString();
    }
}

/// <summary>Validation rules for uploaded images.</summary>
public static class ImageRules
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxBytes = 5 * 1024 * 1024;

    /// <returns>An error message, or null when the file is acceptable.</returns>
    public static string? Validate(IFormFile? file, out string extension)
    {
        extension = "";
        if (file is null || file.Length == 0) return "Please choose an image file.";
        if (file.Length > MaxBytes) return "The image must be 5 MB or smaller.";
        extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return "Only JPG, PNG or WEBP images are allowed.";
        return null;
    }
}
