namespace Nagent.Core.Persistence;

public sealed class SessionMetadata
{
    public required string SessionId { get; init; }
    public required List<string> ToolFiles { get; init; }
    public string? LogFilePath { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}
