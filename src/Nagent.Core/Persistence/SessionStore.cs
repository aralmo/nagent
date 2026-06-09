using System.Text.Json;

namespace Nagent.Core.Persistence;

public sealed class SessionStore : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string GetSessionFilePath(string workingPath, string sessionId) =>
        Path.Combine(workingPath, ".agent", "sessions", $"{sessionId}.json");

    public static string GetIndexFilePath(string sessionId)
    {
        var indexDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "nagent",
            "session-index");
        return Path.Combine(indexDir, $"{sessionId}.txt");
    }

    public async Task CreateSessionAsync(ConversationSession session, CancellationToken cancellationToken = default)
    {
        await SaveSessionAsync(session, cancellationToken);
    }

    public async Task SaveSessionAsync(ConversationSession session, CancellationToken cancellationToken = default)
    {
        var sessionPath = GetSessionFilePath(session.WorkingPath, session.SessionId);
        var sessionsDir = Path.GetDirectoryName(sessionPath)!;
        Directory.CreateDirectory(sessionsDir);

        var tempPath = sessionPath + ".tmp";
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, sessionPath, overwrite: true);

        var indexPath = GetIndexFilePath(session.SessionId);
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
        await File.WriteAllTextAsync(indexPath, sessionPath, cancellationToken);
    }

    public ConversationSession? TryLoadSession(string sessionId)
    {
        var indexPath = GetIndexFilePath(sessionId);
        if (!File.Exists(indexPath))
        {
            return null;
        }

        var sessionPath = File.ReadAllText(indexPath).Trim();
        if (!File.Exists(sessionPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ConversationSession>(File.ReadAllText(sessionPath), JsonOptions);
    }
}
