using Nagent.Core.Domain;

namespace Nagent.Core.Persistence;

public static class AgentContextMapper
{
    public static ConversationSession ToSession(
        AgentContext context,
        SessionMetadata metadata,
        ConversationSession? existing = null)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ConversationSession
        {
            SessionId = metadata.SessionId,
            CreatedAt = existing?.CreatedAt ?? metadata.CreatedAt,
            UpdatedAt = now,
            WorkingPath = context.WorkingPath,
            TemplatePath = context.TemplatePath ?? string.Empty,
            InitialPrompt = context.InitialPrompt,
            ToolFiles = [.. metadata.ToolFiles],
            LogFilePath = metadata.LogFilePath,
            ProgramCounter = context.ProgramCounter,
            History = context.History.Select(SerializeMessage).ToList(),
            Variables = new Dictionary<string, string>(context.Variables, StringComparer.OrdinalIgnoreCase),
            ActiveToolNames = [.. context.ActiveToolNames],
            ActiveToolEntries = [.. context.ActiveToolEntries],
            ModelFallbacks = context.ModelFallbacks.Select(m => m.ToString()).ToList(),
            CurrentRole = context.CurrentRole?.ToApiString(),
            CurrentBuffer = context.CurrentBuffer,
            InitialPromptConsumed = context.InitialPromptConsumed,
            InitialHistoryDisplayed = context.InitialHistoryDisplayed,
            HandoverPerformed = context.HandoverPerformed
        };

        return session;
    }

    public static void ApplyToContext(ConversationSession session, AgentContext context)
    {
        context.TemplatePath = session.TemplatePath;
        context.InitialPrompt = session.InitialPrompt;
        context.ProgramCounter = session.ProgramCounter;
        context.CurrentBuffer = session.CurrentBuffer;
        context.InitialPromptConsumed = session.InitialPromptConsumed;
        context.InitialHistoryDisplayed = session.InitialHistoryDisplayed;
        context.HandoverPerformed = session.HandoverPerformed;
        context.ActiveToolNames = [.. session.ActiveToolNames];
        context.ActiveToolEntries = [.. session.ActiveToolEntries];
        context.ModelFallbacks = session.ModelFallbacks.Select(ModelRef.Parse).ToList();

        context.History.Clear();
        foreach (var message in session.History)
        {
            context.History.Add(DeserializeMessage(message));
        }

        context.Variables.Clear();
        foreach (var (key, value) in session.Variables)
        {
            context.Variables[key] = value;
        }

        context.CurrentRole = session.CurrentRole is null
            ? null
            : ChatRoleExtensions.FromApiString(session.CurrentRole);
    }

    private static SerializedChatMessage SerializeMessage(ChatMessage message) => new()
    {
        Role = message.Role.ToApiString(),
        Content = message.Content,
        ToolCallId = message.ToolCallId,
        Name = message.Name,
        ToolCalls = message.ToolCalls?.Select(t => new SerializedToolCall
        {
            Id = t.Id,
            Name = t.Name,
            ArgumentsJson = t.ArgumentsJson
        }).ToList()
    };

    private static ChatMessage DeserializeMessage(SerializedChatMessage message) => new()
    {
        Role = ChatRoleExtensions.FromApiString(message.Role),
        Content = message.Content,
        ToolCallId = message.ToolCallId,
        Name = message.Name,
        ToolCalls = message.ToolCalls?.Select(t => new ToolCall
        {
            Id = t.Id,
            Name = t.Name,
            ArgumentsJson = t.ArgumentsJson
        }).ToList()
    };
}
