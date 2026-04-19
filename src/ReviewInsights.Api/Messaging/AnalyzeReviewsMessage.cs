namespace ReviewInsights.Api.Messaging;

public class AnalyzeReviewsMessage
{
    public string TaskType { get; set; } = "analyze_reviews";
    public Guid UploadId { get; set; }
    public List<ReviewInput> Reviews { get; set; } = [];
}

public class ReviewInput
{
    public Guid Id { get; set; }
    public int ClothingId { get; set; }
    public int Age { get; set; }
    public string? Title { get; set; }
    public string? ReviewText { get; set; }
    public int Rating { get; set; }
    public bool RecommendedInd { get; set; }
    public string? DivisionName { get; set; }
    public string? DepartmentName { get; set; }
    public string? ClassName { get; set; }
}
