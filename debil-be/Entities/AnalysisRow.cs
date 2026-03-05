namespace debil_be.Entities;

public class AnalysisRow
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
    public int RowIndex { get; set; }
    public Dictionary<string, object?> InputData { get; set; } = new();
    public Dictionary<string, object?> OutputData { get; set; } = new();

    public Analysis Analysis { get; set; } = null!;
}
