using Nagent.Core.Domain;

namespace Nagent.Core.Hosting;

public interface IAgentHost
{
    Task<string?> PromptUserAsync(CancellationToken cancellationToken = default);
    Task<string?> PromptChoiceAsync(
        string message,
        string optionYes,
        string optionNo,
        CancellationToken cancellationToken = default);
    Task WriteStreamDeltaAsync(string delta, CancellationToken cancellationToken = default);
    Task WriteThinkingDeltaAsync(string delta, CancellationToken cancellationToken = default);
    Task WriteToolCallAsync(string name, string argumentsJson, CancellationToken cancellationToken = default);
    Task WriteToolResponseAsync(string name, string content, CancellationToken cancellationToken = default);
    Task WriteSystemMessageAsync(string message, CancellationToken cancellationToken = default);
    Task WriteHistoryMessageAsync(ChatRole role, string content, CancellationToken cancellationToken = default);
    Task WaitForContinueAsync(CancellationToken cancellationToken = default);
}
