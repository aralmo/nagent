using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CustomAgents.Core.Domain;

namespace CustomAgents.Core.Providers;

public sealed class OpenRouterProvider(HttpClient httpClient) : IModelProvider
{
    public string ProviderName => "openrouter";

    public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ProviderException("OPENROUTER_API_KEY environment variable is not set.");
        }

        var payload = BuildPayload(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ProviderException(
                $"OpenRouter request failed ({(int)response.StatusCode}): {body}",
                (int)response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

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
            if (data == "[DONE]")
            {
                yield break;
            }

            foreach (var chunk in ParseSseChunk(data))
            {
                yield return chunk;
            }
        }
    }

    private static JsonObject BuildPayload(ChatCompletionRequest request)
    {
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            var obj = new JsonObject
            {
                ["role"] = message.Role.ToApiString(),
                ["content"] = message.Content
            };

            if (message.ToolCallId is not null)
            {
                obj["tool_call_id"] = message.ToolCallId;
            }

            if (message.Name is not null)
            {
                obj["name"] = message.Name;
            }

            if (message.ToolCalls is { Count: > 0 })
            {
                var calls = new JsonArray();
                foreach (var call in message.ToolCalls)
                {
                    calls.Add(new JsonObject
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["arguments"] = call.ArgumentsJson
                        }
                    });
                }

                obj["tool_calls"] = calls;
            }

            messages.Add(obj);
        }

        var payload = new JsonObject
        {
            ["model"] = request.Model.ModelName,
            ["messages"] = messages,
            ["stream"] = true
        };

        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = tool.Parameters.DeepClone()
                    }
                });
            }

            payload["tools"] = tools;
        }

        return payload;
    }

    private static IEnumerable<StreamChunk> ParseSseChunk(string data)
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

        var delta = node?["choices"]?[0]?["delta"];
        if (delta is null)
        {
            yield break;
        }

        var reasoning = delta["reasoning"]?.GetValue<string>()
            ?? delta["reasoning_content"]?.GetValue<string>()
            ?? delta["thinking"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(reasoning))
        {
            yield return new StreamChunk { ReasoningDelta = reasoning };
        }

        var text = delta["content"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(text))
        {
            yield return new StreamChunk { TextDelta = text };
        }

        var toolCalls = delta["tool_calls"]?.AsArray();
        if (toolCalls is null)
        {
            yield break;
        }

        var deltas = new List<ToolCallDelta>();
        foreach (var callNode in toolCalls)
        {
            if (callNode is null)
            {
                continue;
            }

            deltas.Add(new ToolCallDelta
            {
                Index = callNode["index"]?.GetValue<int>() ?? 0,
                Id = callNode["id"]?.GetValue<string>(),
                Name = callNode["function"]?["name"]?.GetValue<string>(),
                ArgumentsDelta = callNode["function"]?["arguments"]?.GetValue<string>()
            });
        }

        if (deltas.Count > 0)
        {
            yield return new StreamChunk { ToolCallDeltas = deltas };
        }
    }
}
