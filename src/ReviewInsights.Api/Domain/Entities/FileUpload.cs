using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Domain.Entities;

public class FileUpload
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public UploadStatus Status { get; set; } = UploadStatus.Uploading;
    public int TotalRecords { get; set; }
    public int AnalyzedRecords { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public List<Review> Reviews { get; set; } = [];
}
