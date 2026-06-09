using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nagent.Core.Shell;

public static class ShellCommandParser
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public static string Parse(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            throw new ArgumentException("shell requires 'command'.");
        }

        var trimmed = argumentsJson.Trim();
        string? command = null;

        try
        {
            var node = JsonNode.Parse(trimmed);
            command = node switch
            {
                JsonObject obj => ExtractFromObject(obj),
                JsonValue value => value.GetValue<string>(),
                _ => node?.ToJsonString()
            };
        }
        catch (JsonException)
        {
            command = trimmed;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("shell requires 'command'.");
        }

        return CommandUnescape.UnescapeFully(command);
    }

    public static string Parse(JsonObject arguments) =>
        CommandUnescape.UnescapeFully(ExtractFromObject(arguments)
            ?? throw new ArgumentException("shell requires 'command'."));

    public static TimeSpan ParseTimeout(JsonObject arguments) =>
        ParseTimeoutFromNode(arguments.TryGetPropertyValue("timeout", out var node) ? node : null);

    public static TimeSpan ParseTimeout(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return DefaultTimeout;
        }

        try
        {
            var node = JsonNode.Parse(argumentsJson.Trim());
            return node switch
            {
                JsonObject obj => ParseTimeout(obj),
                _ => DefaultTimeout
            };
        }
        catch (JsonException)
        {
            return DefaultTimeout;
        }
    }

    private static TimeSpan ParseTimeoutFromNode(JsonNode? node)
    {
        if (node is null)
        {
            return DefaultTimeout;
        }

        var seconds = node switch
        {
            JsonValue value when value.TryGetValue<double>(out var d) => d,
            JsonValue value when value.TryGetValue<int>(out var i) => i,
            _ => throw new ArgumentException("shell 'timeout' must be a number of seconds.")
        };

        if (seconds <= 0)
        {
            throw new ArgumentException("shell 'timeout' must be positive.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static string? ExtractFromObject(JsonObject arguments)
    {
        if (arguments.TryGetPropertyValue("command", out var commandNode) && commandNode is not null)
        {
            return commandNode switch
            {
                JsonValue value => value.GetValue<string>(),
                _ => commandNode.ToJsonString() is { } json
                    ? JsonSerializer.Deserialize<string>(json)
                    : null
            };
        }

        return null;
    }
}
