using ReviewInsights.Api.Domain.Enums;
using ReviewInsights.Api.Domain.ValueObjects;

namespace ReviewInsights.Api.Domain.Entities;

public class Review
{
    public Guid Id { get; set; }
    public int ClothingId { get; set; }
    public int Age { get; set; }
    public string? Title { get; set; }
    public string? ReviewText { get; set; }
    public int Rating { get; set; }
    public bool RecommendedInd { get; set; }
    public int PositiveFeedbackCount { get; set; }
    public string? DivisionName { get; set; }
    public string? DepartmentName { get; set; }
    public string? ClassName { get; set; }

    public Sentiment? OverallSentiment { get; set; }
    public List<AspectSentiment> AspectSentiments { get; set; } = [];
    public int? ChurnProbability { get; set; }
    public List<ChurnCause> ChurnCauses { get; set; } = [];
    public Priority? Priority { get; set; }

    public Guid UploadId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AnalyzedAt { get; set; }

    public FileUpload Upload { get; set; } = null!;
}
