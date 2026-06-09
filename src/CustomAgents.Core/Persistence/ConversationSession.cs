namespace CustomAgents.Core.Persistence;

public sealed class ConversationSession
{
    public required string SessionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public required string WorkingPath { get; set; }
    public required string TemplatePath { get; set; }
    public string? InitialPrompt { get; set; }
    public List<string> ToolFiles { get; set; } = [];
    public string? LogFilePath { get; set; }
    public int ProgramCounter { get; set; }
    public List<SerializedChatMessage> History { get; set; } = [];
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ActiveToolNames { get; set; } = [];
    public List<string> ActiveToolEntries { get; set; } = [];
    public List<string> ModelFallbacks { get; set; } = [];
    public string? CurrentRole { get; set; }
    public string CurrentBuffer { get; set; } = string.Empty;
    public bool InitialPromptConsumed { get; set; }
    public bool InitialHistoryDisplayed { get; set; }
    public bool HandoverPerformed { get; set; }
}

public sealed class SerializedChatMessage
{
    public required string Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ToolCallId { get; set; }
    public string? Name { get; set; }
    public List<SerializedToolCall>? ToolCalls { get; set; }
}

public sealed class SerializedToolCall
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string ArgumentsJson { get; set; }
}
