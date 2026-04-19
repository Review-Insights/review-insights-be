using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Features.Uploads.Dtos;

public class FileUploadDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public UploadStatus Status { get; set; }
    public int TotalRecords { get; set; }
    public int AnalyzedRecords { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
