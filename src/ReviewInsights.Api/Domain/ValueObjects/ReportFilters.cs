namespace ReviewInsights.Api.Domain.ValueObjects;

public class ReportFilters
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? DepartmentName { get; set; }
    public string? DivisionName { get; set; }
    public string? ClassName { get; set; }
    public int? ClothingId { get; set; }
    public int? MinRating { get; set; }
    public int? MaxRating { get; set; }
}
