namespace Nagent.Core.Domain;

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}

public static class ChatRoleExtensions
{
    public static string ToApiString(this ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        ChatRole.Tool => "tool",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public static ChatRole FromTag(string tag) => tag.Trim().ToUpperInvariant() switch
    {
        "SYSTEM" => ChatRole.System,
        "USER" => ChatRole.User,
        "ASSISTANT" => ChatRole.Assistant,
        "TOOL" => ChatRole.Tool,
        _ => throw new ArgumentException($"Unknown role tag: {tag}")
    };

    public static ChatRole FromApiString(string role) => role.Trim().ToLowerInvariant() switch
    {
        "system" => ChatRole.System,
        "user" => ChatRole.User,
        "assistant" => ChatRole.Assistant,
        "tool" => ChatRole.Tool,
        _ => throw new ArgumentException($"Unknown role: {role}")
    };
}
