using Nagent.Core.Domain;

namespace Nagent.Core.Persistence;

public sealed class SessionCheckpointService(
    ISessionStore store,
    SessionMetadata metadata,
    ConversationSession? existingSession = null)
{
    private ConversationSession? _lastSession = existingSession;

    public string SessionId => metadata.SessionId;

    public async Task SaveAsync(
        AgentContext context,
        int? programCounterOverride = null,
        CancellationToken cancellationToken = default)
    {
        var session = AgentContextMapper.ToSession(context, metadata, _lastSession);
        if (programCounterOverride.HasValue)
        {
            session.ProgramCounter = programCounterOverride.Value;
        }

        session.UpdatedAt = DateTimeOffset.UtcNow;

        if (_lastSession is null)
        {
            await store.CreateSessionAsync(session, cancellationToken);
        }
        else
        {
            await store.SaveSessionAsync(session, cancellationToken);
        }

        _lastSession = session;
    }
}
