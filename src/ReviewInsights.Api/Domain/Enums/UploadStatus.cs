using System.Runtime.Serialization;

namespace ReviewInsights.Api.Domain.Enums;

public enum UploadStatus
{
    [EnumMember(Value = "uploading")]
    Uploading,

    [EnumMember(Value = "analyzing")]
    Analyzing,

    [EnumMember(Value = "done")]
    Done,

    [EnumMember(Value = "error")]
    Error
}
