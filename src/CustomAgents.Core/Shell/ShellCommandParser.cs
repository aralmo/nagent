using System.Text.Json;
using System.Text.Json.Nodes;

namespace CustomAgents.Core.Shell;

public static class ShellCommandParser
{
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
