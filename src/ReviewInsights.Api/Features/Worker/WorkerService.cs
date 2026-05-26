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
        _logger.LogInformation(
            "Processing analyze results for upload {UploadId}: {ResultCount} results received",
            uploadId, request.Results.Count);

        var upload = await _db.FileUploads.FirstOrDefaultAsync(u => u.Id == uploadId, ct)
                     ?? throw new NotFoundException($"Upload {uploadId} not found");

        if (request.Results.Count == 0)
        {
            _logger.LogWarning("Received empty results batch for upload {UploadId}", uploadId);
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
                _logger.LogWarning(
                    "Worker sent unknown ReviewId={ReviewId} for upload {UploadId} — skipping",
                    result.ReviewId, uploadId);
                continue;
            }
            if (review.AnalyzedAt is not null)
            {
                continue;
            }
            review.OverallSentiment = result.OverallSentiment;
            review.AspectSentiments = result.AspectSentiments ?? [];
            review.Priority = result.Priority;
            review.PriorityRule = result.PriorityRule;
            review.PriorityReason = result.PriorityReason;
            review.AnalyzedAt = now;
            patchedThisCall++;
        }

        upload.AnalyzedRecords = Math.Min(upload.TotalRecords, upload.AnalyzedRecords + patchedThisCall);

        if (upload.AnalyzedRecords >= upload.TotalRecords && upload.TotalRecords > 0)
        {
            upload.Status = UploadStatus.Done;
            upload.CompletedAt = now;
            _logger.LogInformation(
                "Upload {UploadId} analysis completed: {TotalRecords} records analyzed",
                uploadId, upload.TotalRecords);
        }
        else if (upload.Status == UploadStatus.Uploading)
        {
            upload.Status = UploadStatus.Analyzing;
        }

        _logger.LogInformation(
            "Upload {UploadId} progress: {AnalyzedRecords}/{TotalRecords} analyzed (+{PatchedThisCall} this batch)",
            uploadId, upload.AnalyzedRecords, upload.TotalRecords, patchedThisCall);

        await _db.SaveChangesAsync(ct);
    }

    public async Task PatchReportResultAsync(Guid reportId, WorkerReportResultRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Applying report result for report {ReportId}", reportId);

        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, ct)
                     ?? throw new NotFoundException($"Report {reportId} not found");

        report.Scope = request.Scope;
        report.Summary = request.Summary;
        report.Insights = request.Insights ?? [];
        report.Suggestions = request.Suggestions ?? [];
        report.Status = ReportStatus.Completed;
        report.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Report {ReportId} completed: {InsightCount} insights, {SuggestionCount} suggestions",
            reportId, report.Insights.Count, report.Suggestions.Count);
    }

    public async Task RegisterUploadErrorAsync(Guid uploadId, WorkerErrorRequest request, CancellationToken ct)
    {
        _logger.LogWarning(
            "Registering upload error for {UploadId}: {ErrorMessage}", uploadId, request.ErrorMessage);

        var upload = await _db.FileUploads.FirstOrDefaultAsync(u => u.Id == uploadId, ct)
                     ?? throw new NotFoundException($"Upload {uploadId} not found");

        upload.Status = UploadStatus.Error;
        upload.ErrorMessage = request.ErrorMessage;
        upload.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task RegisterReportErrorAsync(Guid reportId, WorkerErrorRequest request, CancellationToken ct)
    {
        _logger.LogWarning(
            "Registering report error for {ReportId}: {ErrorMessage}", reportId, request.ErrorMessage);

        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, ct)
                     ?? throw new NotFoundException($"Report {reportId} not found");

        report.Status = ReportStatus.Failed;
        report.ErrorMessage = request.ErrorMessage;
        report.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
