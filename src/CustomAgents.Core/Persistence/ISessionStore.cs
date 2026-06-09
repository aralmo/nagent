namespace CustomAgents.Core.Persistence;

public interface ISessionStore
{
    Task CreateSessionAsync(ConversationSession session, CancellationToken cancellationToken = default);
    Task SaveSessionAsync(ConversationSession session, CancellationToken cancellationToken = default);
    ConversationSession? TryLoadSession(string sessionId);
}
