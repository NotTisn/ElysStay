using Application.Common.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.FileUpload;

/// <summary>
/// Uploads files to Cloudinary CDN.
/// Free tier: 25 credits/month (~25 GB storage + transforms).
/// If not configured, all operations are no-ops that return empty strings.
/// </summary>
public sealed class CloudinaryUploadService : IFileUploadService
{
    private readonly Cloudinary? _cloudinary;
    private readonly ILogger<CloudinaryUploadService> _logger;
    private readonly bool _enabled;

    public CloudinaryUploadService(IConfiguration configuration, ILogger<CloudinaryUploadService> logger)
    {
        _logger = logger;

        var section = configuration.GetSection("Cloudinary");
        var cloudName = section["CloudName"];
        var apiKey = section["ApiKey"];
        var apiSecret = section["ApiSecret"];

        _enabled = !string.IsNullOrWhiteSpace(cloudName)
                && !string.IsNullOrWhiteSpace(apiKey)
                && !string.IsNullOrWhiteSpace(apiSecret);

        if (_enabled)
        {
            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }
        else
        {
            _logger.LogInformation("Cloudinary not configured — file uploads will be disabled.");
        }
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string folder, CancellationToken ct = default)
    {
        if (!_enabled || _cloudinary is null)
        {
            _logger.LogWarning("Upload skipped (Cloudinary not configured): {FileName} → {Folder}", fileName, folder);
            return string.Empty;
        }

        try
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = $"elysstay/{folder}",
                Transformation = folder == "avatars"
                    ? new Transformation().Width(512).Height(512).Crop("fill").Gravity("face")
                    : null
            };

            var result = await _cloudinary.UploadAsync(uploadParams, ct);

            if (result.Error != null)
            {
                _logger.LogWarning("Cloudinary upload failed: {Error}", result.Error.Message);
                return string.Empty;
            }

            _logger.LogInformation("Uploaded {FileName} → {Url}", fileName, result.SecureUrl);
            return result.SecureUrl.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cloudinary upload error: {FileName}", fileName);
            return string.Empty;
        }
    }

    public async Task DeleteAsync(string publicId, CancellationToken ct = default)
    {
        if (!_enabled || _cloudinary is null || string.IsNullOrWhiteSpace(publicId))
            return;

        try
        {
            await _cloudinary.DestroyAsync(new DeletionParams(publicId) { ResourceType = ResourceType.Image });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cloudinary delete error: {PublicId}", publicId);
        }
    }
}
