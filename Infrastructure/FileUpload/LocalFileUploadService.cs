using Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.FileUpload;

public sealed class LocalFileUploadService : IFileUploadService
{
    private readonly string _uploadFolder;
    private readonly ILogger<LocalFileUploadService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LocalFileUploadService(
        IWebHostEnvironment env, 
        ILogger<LocalFileUploadService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        
        var webRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        _uploadFolder = Path.Combine(webRoot, "uploads");

        if (!Directory.Exists(_uploadFolder))
        {
            Directory.CreateDirectory(_uploadFolder);
        }
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string folder, CancellationToken ct = default)
    {
        try
        {
            var folderPath = Path.Combine(_uploadFolder, folder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
            var filePath = Path.Combine(folderPath, uniqueFileName);

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await stream.CopyToAsync(fileStream, ct);

            _logger.LogInformation("Uploaded local file {FileName} -> {FilePath}", fileName, filePath);

            var request = _httpContextAccessor.HttpContext?.Request;
            var baseUrl = request != null 
                ? $"{request.Scheme}://{request.Host}{request.PathBase}" 
                : "";

            // Return absolute URL.
            var url = $"{baseUrl}/uploads/{folder}/{uniqueFileName}";
            return url.Replace("\\", "/");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local file upload error for {FileName}", fileName);
            return string.Empty;
        }
    }

    public Task DeleteAsync(string publicId, CancellationToken ct = default)
    {
        try
        {
            if (Uri.TryCreate(publicId, UriKind.Absolute, out var uri))
            {
                var path = uri.AbsolutePath;
                if (path.StartsWith("/uploads/"))
                {
                    path = path.Substring("/uploads/".Length);
                }
                
                var filePath = Path.Combine(_uploadFolder, path.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local file delete error for {PublicId}", publicId);
        }
        return Task.CompletedTask;
    }
}
