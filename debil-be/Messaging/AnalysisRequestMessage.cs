namespace debil_be.Messaging;

public class AnalysisRequestMessage
{
    public Guid AnalysisId { get; set; }
    public Guid BlueprintId { get; set; }
    public string FileStorageKey { get; set; } = string.Empty;
}
