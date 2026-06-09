namespace Nagent.Core.Domain;

public sealed class AgentContext
{
    public required string WorkingPath { get; init; }
    public string? TemplatePath { get; set; }
    public List<ChatMessage> History { get; } = [];
    public ChatRole? CurrentRole { get; set; }
    public string CurrentBuffer { get; set; } = string.Empty;
    public List<string> ActiveToolNames { get; set; } = [];
    public List<string> ActiveToolEntries { get; set; } = [];
    public List<ModelRef> ModelFallbacks { get; set; } = [];
    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["workingPath"] = string.Empty,
        ["datetime"] = string.Empty,
        ["prompt"] = string.Empty,
        ["completion"] = string.Empty
    };

    public int ProgramCounter { get; set; }
    public string? InitialPrompt { get; set; }
    public bool InitialPromptConsumed { get; set; }
    public bool InitialHistoryDisplayed { get; set; }
    public bool HandoverPerformed { get; set; }
    public bool SuppressOutput { get; set; }

    public void InitializeVariables()
    {
        Variables["workingPath"] = WorkingPath;
        Variables["datetime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Variables["prompt"] = string.IsNullOrWhiteSpace(InitialPrompt) ? string.Empty : InitialPrompt;
        Variables["completion"] = string.Empty;
    }

    public void RefreshDateTime() =>
        Variables["datetime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
