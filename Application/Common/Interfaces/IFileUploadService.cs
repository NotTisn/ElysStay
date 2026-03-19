namespace Application.Common.Interfaces;

/// <summary>
/// Uploads files to cloud storage (Cloudinary).
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Uploads a file stream to cloud storage.
    /// Returns the public URL of the uploaded file.
    /// </summary>
    /// <param name="stream">File content stream</param>
    /// <param name="fileName">Original file name (used for format detection)</param>
    /// <param name="folder">Logical folder name (e.g. "avatars", "cccd", "receipts")</param>
    /// <param name="ct">Cancellation token</param>
    Task<string> UploadAsync(Stream stream, string fileName, string folder, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file from cloud storage by its public ID.
    /// </summary>
    Task DeleteAsync(string publicId, CancellationToken ct = default);
}
