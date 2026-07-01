using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nagent.Core.Domain;

namespace Nagent.Core.Providers;

public sealed class AnthropicProvider(HttpClient httpClient) : IModelProvider
{
    private const string ApiVersion = "2023-06-01";
    private const int DefaultMaxTokens = 8192;

    public string ProviderName => "anthropic";

    public TimeSpan? GetRetryDelay(ProviderException ex)
    {
        if (ex.StatusCode != 429)
        {
            return null;
        }

        if (ex.ResponseHeaders is not null)
        {
            foreach (var (key, value) in ex.ResponseHeaders)
            {
                if (key.Equals("retry-after", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(value, out var seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
        }

        return null;
    }

    public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ProviderException("ANTHROPIC_API_KEY environment variable is not set.");
        }

        var payload = BuildPayload(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        httpRequest.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ProviderException(
                $"Anthropic request failed ({(int)response.StatusCode}): {body}",
                (int)response.StatusCode,
                body,
                ProviderHttpHelper.CollectHeaders(response));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var toolBlocks = new Dictionary<int, ToolBlockState>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            foreach (var chunk in ParseSseData(data, toolBlocks))
            {
                yield return chunk;
            }
        }
    }

    private static JsonObject BuildPayload(ChatCompletionRequest request)
    {
        var (system, messages) = BuildAnthropicMessages(request.Messages);

        var payload = new JsonObject
        {
            ["model"] = ResolveModel(request.Model.ModelName),
            ["max_tokens"] = DefaultMaxTokens,
            ["messages"] = messages,
            ["stream"] = true
        };

        if (!string.IsNullOrEmpty(system))
        {
            payload["system"] = system;
        }

        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["input_schema"] = tool.Parameters.DeepClone()
                });
            }

            payload["tools"] = tools;
        }

        return payload;
    }

    private static (string System, JsonArray Messages) BuildAnthropicMessages(IReadOnlyList<ChatMessage> messages)
    {
        var systemParts = new List<string>();
        var anthropicMessages = new JsonArray();
        var pendingToolResults = new List<JsonObject>();

        void FlushToolResults()
        {
            if (pendingToolResults.Count == 0)
            {
                return;
            }

            var blocks = new JsonArray();
            foreach (var result in pendingToolResults)
            {
                blocks.Add(result);
            }

            anthropicMessages.Add(new JsonObject
            {
                ["role"] = "user",
                ["content"] = blocks
            });
            pendingToolResults.Clear();
        }

        foreach (var message in messages)
        {
            switch (message.Role)
            {
                case ChatRole.System:
                    if (!string.IsNullOrEmpty(message.Content))
                    {
                        systemParts.Add(message.Content);
                    }

                    break;

                case ChatRole.Tool:
                    pendingToolResults.Add(new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = message.ToolCallId ?? string.Empty,
                        ["content"] = message.Content
                    });
                    break;

                case ChatRole.User:
                    if (pendingToolResults.Count > 0)
                    {
                        var blocks = new JsonArray();
                        foreach (var result in pendingToolResults)
                        {
                            blocks.Add(result);
                        }

                        if (!string.IsNullOrEmpty(message.Content))
                        {
                            blocks.Add(new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = message.Content
                            });
                        }

                        anthropicMessages.Add(new JsonObject
                        {
                            ["role"] = "user",
                            ["content"] = blocks
                        });
                        pendingToolResults.Clear();
                    }
                    else
                    {
                        anthropicMessages.Add(new JsonObject
                        {
                            ["role"] = "user",
                            ["content"] = message.Content
                        });
                    }

                    break;

                case ChatRole.Assistant:
                    FlushToolResults();

                    if (message.ToolCalls is { Count: > 0 })
                    {
                        var blocks = new JsonArray();
                        if (!string.IsNullOrEmpty(message.Content))
                        {
                            blocks.Add(new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = message.Content
                            });
                        }

                        foreach (var call in message.ToolCalls)
                        {
                            var input = JsonNode.Parse(call.ArgumentsJson) ?? new JsonObject();
                            blocks.Add(new JsonObject
                            {
                                ["type"] = "tool_use",
                                ["id"] = call.Id,
                                ["name"] = call.Name,
                                ["input"] = input
                            });
                        }

                        anthropicMessages.Add(new JsonObject
                        {
                            ["role"] = "assistant",
                            ["content"] = blocks
                        });
                    }
                    else
                    {
                        anthropicMessages.Add(new JsonObject
                        {
                            ["role"] = "assistant",
                            ["content"] = message.Content
                        });
                    }

                    break;
            }
        }

        FlushToolResults();
        return (string.Join("\n\n", systemParts), anthropicMessages);
    }

    private static string ResolveModel(string name) =>
        name.Trim().ToLowerInvariant() switch
        {
            "sonnet" => "claude-sonnet-4-6",
            "opus" => "claude-opus-4-6",
            "haiku" => "claude-haiku-4-5",
            _ => name
        };

    private static IEnumerable<StreamChunk> ParseSseData(string data, Dictionary<int, ToolBlockState> toolBlocks)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(data);
        }
        catch (JsonException)
        {
            yield break;
        }

        if (node is null)
        {
            yield break;
        }

        var type = node["type"]?.GetValue<string>();
        if (type is null)
        {
            yield break;
        }

        switch (type)
        {
            case "error":
                var errorMessage = node["error"]?["message"]?.GetValue<string>()
                    ?? node["message"]?.GetValue<string>()
                    ?? data;
                throw new ProviderException($"Anthropic stream error: {errorMessage}");

            case "content_block_start":
            {
                var index = node["index"]?.GetValue<int>() ?? 0;
                var block = node["content_block"];
                if (block?["type"]?.GetValue<string>() == "tool_use")
                {
                    var id = block["id"]?.GetValue<string>();
                    var name = block["name"]?.GetValue<string>();
                    toolBlocks[index] = new ToolBlockState { Id = id, Name = name };

                    yield return new StreamChunk
                    {
                        ToolCallDeltas =
                        [
                            new ToolCallDelta
                            {
                                Index = index,
                                Id = id,
                                Name = name
                            }
                        ]
                    };
                }

                yield break;
            }

            case "content_block_delta":
            {
                var index = node["index"]?.GetValue<int>() ?? 0;
                var delta = node["delta"];
                if (delta is null)
                {
                    yield break;
                }

                var deltaType = delta["type"]?.GetValue<string>();
                switch (deltaType)
                {
                    case "text_delta":
                        var text = delta["text"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(text))
                        {
                            yield return new StreamChunk { TextDelta = text };
                        }

                        break;

                    case "thinking_delta":
                        var thinking = delta["thinking"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(thinking))
                        {
                            yield return new StreamChunk { ReasoningDelta = thinking };
                        }

                        break;

                    case "input_json_delta":
                        var partial = delta["partial_json"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(partial))
                        {
                            yield return new StreamChunk
                            {
                                ToolCallDeltas =
                                [
                                    new ToolCallDelta
                                    {
                                        Index = index,
                                        ArgumentsDelta = partial
                                    }
                                ]
                            };
                        }

                        break;
                }

                yield break;
            }

            case "content_block_stop":
            {
                var index = node["index"]?.GetValue<int>() ?? 0;
                toolBlocks.Remove(index);
                yield break;
            }
        }
    }

    private sealed class ToolBlockState
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
    }
}
