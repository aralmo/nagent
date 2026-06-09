namespace Nagent.Core.Domain;

public sealed class ToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ArgumentsJson { get; init; }
}

public sealed class ChatMessage
{
    public required ChatRole Role { get; init; }
    public string Content { get; set; } = string.Empty;
    public string? ToolCallId { get; init; }
    public string? Name { get; init; }
    public List<ToolCall>? ToolCalls { get; set; }

    public ChatMessage Clone() => new()
    {
        Role = Role,
        Content = Content,
        ToolCallId = ToolCallId,
        Name = Name,
        ToolCalls = ToolCalls?.Select(t => new ToolCall
        {
            Id = t.Id,
            Name = t.Name,
            ArgumentsJson = t.ArgumentsJson
        }).ToList()
    };
}
