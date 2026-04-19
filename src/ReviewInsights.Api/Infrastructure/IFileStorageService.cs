namespace ReviewInsights.Api.Infrastructure;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadFileAsync(string fileKey, CancellationToken ct = default);
    Task DeleteFileAsync(string fileKey, CancellationToken ct = default);
}
