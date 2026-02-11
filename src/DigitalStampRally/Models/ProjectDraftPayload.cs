namespace DigitalStampRally.Models;

public sealed class ProjectDraftPayload
{
    public string Json { get; init; } = "";
    public DraftImagePayload? EventImage { get; init; }
}

public sealed class DraftImagePayload
{
    public byte[] Bytes { get; init; } = Array.Empty<byte>();
    public string FileName { get; init; } = "event";
    public string ContentType { get; init; } = "application/octet-stream";
}
