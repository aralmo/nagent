using System.Text;
using CustomAgents.Core.Domain;
using CustomAgents.Core.Hosting;
using CustomAgents.Core.Logging;

namespace CustomAgents.Core.Providers;

public sealed class ModelRequestService(
    ProviderRegistry providerRegistry,
    IAgentHost host,
    IConversationLogger logger)
{
    private const int PostFallbackRetries = 4;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    public async Task<CompletionResult> CompleteAsync(
        IReadOnlyList<ModelRef> fallbacks,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolSchema>? tools,
        AgentContext? context = null,
        bool updateCompletion = false,
        bool streamToHost = true,
        CancellationToken cancellationToken = default)
    {
        var requestLog = new
        {
            type = "request",
            timestamp = DateTimeOffset.UtcNow,
            models = fallbacks.Select(m => m.ToString()).ToArray(),
            messages = messages.Select(m => new
            {
                role = m.Role.ToApiString(),
                content = m.Content,
                tool_calls = m.ToolCalls?.Select(t => new { t.Id, t.Name, t.ArgumentsJson })
            }),
            tools = tools?.Select(t => t.Name).ToArray()
        };
        await logger.LogAsync(requestLog, cancellationToken);

        ProviderException? lastError = null;
        ModelRef? lastModel = null;

        for (var round = 0; round <= PostFallbackRetries; round++)
        {
            foreach (var model in fallbacks)
            {
                try
                {
                    return await StreamSingleModelAsync(
                        model,
                        messages,
                        tools,
                        context,
                        updateCompletion,
                        streamToHost,
                        cancellationToken);
                }
                catch (ProviderException ex)
                {
                    lastError = ex;
                    lastModel = model;
                    await host.WriteSystemMessageAsync(
                        $"Model {model} failed: {ex.Message}",
                        cancellationToken);
                }
            }

            if (lastError is not null && lastModel is not null && round < PostFallbackRetries)
            {
                var provider = providerRegistry.GetProvider(lastModel.Provider);
                var customDelay = provider.GetRetryDelay(lastError);
                var delay = customDelay ?? RetryDelay;
                var delayMessage = customDelay is not null
                    ? $"All model fallbacks failed. Waiting for {lastModel.Provider} rate limit reset ({delay.TotalSeconds:0} seconds, {round + 1}/{PostFallbackRetries})..."
                    : $"All model fallbacks failed. Retrying in {delay.TotalSeconds:0} seconds ({round + 1}/{PostFallbackRetries})...";
                await host.WriteSystemMessageAsync(delayMessage, cancellationToken);
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            break;
        }

        throw lastError ?? new ProviderException("No model fallbacks configured.");
    }

    private async Task<CompletionResult> StreamSingleModelAsync(
        ModelRef model,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolSchema>? tools,
        AgentContext? context,
        bool updateCompletion,
        bool streamToHost,
        CancellationToken cancellationToken)
    {
        var provider = providerRegistry.GetProvider(model.Provider);
        var request = new ChatCompletionRequest
        {
            Model = model,
            Messages = messages,
            Tools = tools
        };

        var contentBuilder = new StringBuilder();
        var toolBuilders = new Dictionary<int, ToolCallBuilder>();

        await foreach (var chunk in provider.StreamChatAsync(request, cancellationToken))
        {
            if (streamToHost && !string.IsNullOrEmpty(chunk.ReasoningDelta))
            {
                await host.WriteThinkingDeltaAsync(chunk.ReasoningDelta, cancellationToken);
            }

            if (!string.IsNullOrEmpty(chunk.TextDelta))
            {
                contentBuilder.Append(chunk.TextDelta);
                if (streamToHost)
                {
                    await host.WriteStreamDeltaAsync(chunk.TextDelta, cancellationToken);
                }
            }

            if (chunk.ToolCallDeltas is null)
            {
                continue;
            }

            foreach (var delta in chunk.ToolCallDeltas)
            {
                if (!toolBuilders.TryGetValue(delta.Index, out var builder))
                {
                    builder = new ToolCallBuilder();
                    toolBuilders[delta.Index] = builder;
                }

                builder.Apply(delta);
            }
        }

        var toolCalls = toolBuilders
            .OrderBy(kv => kv.Key)
            .Select(kv => kv.Value.Build())
            .Where(tc => tc is not null)
            .Cast<ToolCall>()
            .ToList();

        if (streamToHost)
        {
            foreach (var toolCall in toolCalls)
            {
                await host.WriteToolCallAsync(toolCall.Name, toolCall.ArgumentsJson, cancellationToken);
            }
        }

        var result = new CompletionResult
        {
            Content = contentBuilder.ToString(),
            ToolCalls = toolCalls,
            ModelUsed = model
        };

        if (updateCompletion && context is not null && toolCalls.Count == 0)
        {
            context.Variables["completion"] = result.Content;
        }

        await logger.LogAsync(new
        {
            type = "completion",
            timestamp = DateTimeOffset.UtcNow,
            model = model.ToString(),
            content = result.Content,
            tool_calls = result.ToolCalls.Select(t => new { t.Id, t.Name, t.ArgumentsJson })
        }, cancellationToken);

        return result;
    }

    private sealed class ToolCallBuilder
    {
        private string? _id;
        private string? _name;
        private readonly StringBuilder _arguments = new();

        public void Apply(ToolCallDelta delta)
        {
            if (!string.IsNullOrEmpty(delta.Id))
            {
                _id = delta.Id;
            }

            if (!string.IsNullOrEmpty(delta.Name))
            {
                _name = delta.Name;
            }

            if (!string.IsNullOrEmpty(delta.ArgumentsDelta))
            {
                _arguments.Append(delta.ArgumentsDelta);
            }
        }

        public ToolCall? Build()
        {
            if (string.IsNullOrWhiteSpace(_name))
            {
                return null;
            }

            return new ToolCall
            {
                Id = _id ?? Guid.NewGuid().ToString("N"),
                Name = _name,
                ArgumentsJson = _arguments.Length > 0 ? _arguments.ToString() : "{}"
            };
        }
    }
}
