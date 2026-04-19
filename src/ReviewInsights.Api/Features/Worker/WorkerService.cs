using Microsoft.EntityFrameworkCore;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Data;
using ReviewInsights.Api.Domain.Enums;

namespace ReviewInsights.Api.Features.Worker;

public class WorkerService
{
    private readonly AppDbContext _db;
    private readonly ILogger<WorkerService> _logger;

    public WorkerService(AppDbContext db, ILogger<WorkerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task PatchAnalyzeResultsAsync(Guid uploadId, WorkerAnalyzeResultsRequest request, CancellationToken ct)
    {
        var upload = await _db.FileUploads.FirstOrDefaultAsync(u => u.Id == uploadId, ct)
                     ?? throw new NotFoundException($"Upload {uploadId} not found");

        if (request.Results.Count == 0)
        {
            return;
        }

        var ids = request.Results.Select(r => r.ReviewId).ToHashSet();
        var reviews = await _db.Reviews.Where(r => r.UploadId == uploadId && ids.Contains(r.Id))
            .ToListAsync(ct);

        var byId = reviews.ToDictionary(r => r.Id);
        var patchedThisCall = 0;
        var now = DateTime.UtcNow;

        foreach (var result in request.Results)
        {
            if (!byId.TryGetValue(result.ReviewId, out var review))
            {
                _logger.LogWarning("Worker sent unknown review {ReviewId} for upload {UploadId}", result.ReviewId, uploadId);
                continue;
            }
            if (review.AnalyzedAt is not null)
            {
                continue;
            }
            review.OverallSentiment = result.OverallSentiment;
            review.AspectSentiments = result.AspectSentiments ?? [];
            review.ChurnProbability = result.ChurnProbability;
            review.ChurnCauses = result.ChurnCauses ?? [];
            review.Priority = result.Priority;
            review.AnalyzedAt = now;
            patchedThisCall++;
        }

        upload.AnalyzedRecords = Math.Min(upload.TotalRecords, upload.AnalyzedRecords + patchedThisCall);
        if (upload.AnalyzedRecords >= upload.TotalRecords && upload.TotalRecords > 0)
        {
            upload.Status = UploadStatus.Done;
            upload.CompletedAt = now;
        }
        else if (upload.Status == UploadStatus.Uploading)
        {
            upload.Status = UploadStatus.Analyzing;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task PatchReportResultAsync(Guid reportId, WorkerReportResultRequest request, CancellationToken ct)
    {
        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, ct)
                     ?? throw new NotFoundException($"Report {reportId} not found");

        report.Summary = request.Summary;
        report.Insights = request.Insights ?? [];
        report.Suggestions = request.Suggestions ?? [];
        report.Status = ReportStatus.Completed;
        report.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task RegisterUploadErrorAsync(Guid uploadId, WorkerErrorRequest request, CancellationToken ct)
    {
        var upload = await _db.FileUploads.FirstOrDefaultAsync(u => u.Id == uploadId, ct)
                     ?? throw new NotFoundException($"Upload {uploadId} not found");

        upload.Status = UploadStatus.Error;
        upload.ErrorMessage = request.ErrorMessage;
        upload.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task RegisterReportErrorAsync(Guid reportId, WorkerErrorRequest request, CancellationToken ct)
    {
        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, ct)
                     ?? throw new NotFoundException($"Report {reportId} not found");

        report.Status = ReportStatus.Failed;
        report.ErrorMessage = request.ErrorMessage;
        report.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
