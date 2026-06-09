using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CustomAgents.Core.Domain;

namespace CustomAgents.Core.Providers;

public sealed class OllamaProvider(HttpClient httpClient, string baseUrl = "http://localhost:11434") : IModelProvider
{
    public string ProviderName => "ollama";

    public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/chat")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ProviderException(
                $"Ollama request failed ({(int)response.StatusCode}): {body}",
                (int)response.StatusCode,
                body,
                ProviderHttpHelper.CollectHeaders(response));
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

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            foreach (var chunk in ParseLine(line))
            {
                yield return chunk;
            }

            try
            {
                var node = JsonNode.Parse(line);
                if (node?["done"]?.GetValue<bool>() == true)
                {
                    yield break;
                }
            }
            catch (JsonException)
            {
                // ignore malformed trailing lines
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

            if (message.ToolCalls is { Count: > 0 })
            {
                var calls = new JsonArray();
                foreach (var call in message.ToolCalls)
                {
                    var args = JsonNode.Parse(call.ArgumentsJson) ?? new JsonObject();
                    calls.Add(new JsonObject
                    {
                        ["function"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["arguments"] = args
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

    private static IEnumerable<StreamChunk> ParseLine(string line)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(line);
        }
        catch (JsonException)
        {
            yield break;
        }

        var message = node?["message"];
        if (message is null)
        {
            yield break;
        }

        var thinking = message["thinking"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(thinking))
        {
            yield return new StreamChunk { ReasoningDelta = thinking };
        }

        var text = message["content"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(text))
        {
            yield return new StreamChunk { TextDelta = text };
        }

        var toolCalls = message["tool_calls"]?.AsArray();
        if (toolCalls is null)
        {
            yield break;
        }

        var index = 0;
        var deltas = new List<ToolCallDelta>();
        foreach (var callNode in toolCalls)
        {
            if (callNode is null)
            {
                continue;
            }

            var args = callNode["function"]?["arguments"];
            deltas.Add(new ToolCallDelta
            {
                Index = index++,
                Name = callNode["function"]?["name"]?.GetValue<string>(),
                ArgumentsDelta = args?.ToJsonString()
            });
        }

        if (deltas.Count > 0)
        {
            yield return new StreamChunk { ToolCallDeltas = deltas };
        }
    }
}
