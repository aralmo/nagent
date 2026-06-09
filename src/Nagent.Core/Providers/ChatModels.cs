using System.Text.Json;
using System.Text.Json.Nodes;
using Nagent.Core.Domain;

namespace Nagent.Core.Providers;

public sealed class ToolSchema
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonObject Parameters { get; init; }
}

public sealed class ChatCompletionRequest
{
    public required ModelRef Model { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public IReadOnlyList<ToolSchema>? Tools { get; init; }
}

public sealed class StreamChunk
{
    public string? TextDelta { get; init; }
    public string? ReasoningDelta { get; init; }
    public IReadOnlyList<ToolCallDelta>? ToolCallDeltas { get; init; }
}

public sealed class ToolCallDelta
{
    public int Index { get; init; }
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? ArgumentsDelta { get; init; }
}

public sealed class CompletionResult
{
    public required string Content { get; init; }
    public IReadOnlyList<ToolCall> ToolCalls { get; init; } = [];
    public required ModelRef ModelUsed { get; init; }
}

public interface IModelProvider
{
    string ProviderName { get; }
    IAsyncEnumerable<StreamChunk> StreamChatAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);

    TimeSpan? GetRetryDelay(ProviderException ex) => null;
}

public sealed class ProviderException : Exception
{
    public int? StatusCode { get; }
    public string? ResponseBody { get; }
    public IReadOnlyDictionary<string, string>? ResponseHeaders { get; }

    public ProviderException(
        string message,
        int? statusCode = null,
        string? responseBody = null,
        IReadOnlyDictionary<string, string>? responseHeaders = null,
        Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        ResponseHeaders = responseHeaders;
    }

    public bool IsClientError => StatusCode is >= 400 and < 500;
}
