namespace CustomAgents.Core.Logging;

public interface IConversationLogger
{
    Task LogAsync(object entry, CancellationToken cancellationToken = default);
    Task RotateSessionAsync(string workingPath, CancellationToken cancellationToken = default);
}
