using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Nagent.Core.Domain;
using Nagent.Core.Variables;

namespace Nagent.Core.Tools;

public static partial class CommandTemplateSubstitution
{
    [GeneratedRegex(@"(?<!\{)\$(\w+)(?!\})")]
    private static partial Regex ParameterTokenRegex();

    public static string Substitute(
        string commandTemplate,
        IReadOnlyList<string> definedParameters,
        JsonObject arguments,
        AgentContext? context)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (context is not null)
        {
            foreach (var (key, value) in context.Variables)
            {
                values[key] = value;
            }

            values["history"] = VariableSubstitution.SerializeHistory(context.History);
        }

        foreach (var property in arguments)
        {
            if (property.Value is null)
            {
                continue;
            }

            values[property.Key] = JsonNodeToString(property.Value);
        }

        var definedSet = new HashSet<string>(definedParameters, StringComparer.OrdinalIgnoreCase);
        var missingParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameterName in definedParameters)
        {
            if (!arguments.ContainsKey(parameterName))
            {
                missingParameters.Add(parameterName);
            }
        }

        var result = ParameterTokenRegex().Replace(commandTemplate, match =>
        {
            var name = match.Groups[1].Value;

            if (values.TryGetValue(name, out var value))
            {
                missingParameters.Remove(name);
                return value;
            }

            if (definedSet.Contains(name))
            {
                throw new ArgumentException($"missing required parameter '{name}'");
            }

            return match.Value;
        });

        if (missingParameters.Count > 0)
        {
            var first = missingParameters.First();
            throw new ArgumentException($"missing required parameter '{first}'");
        }

        return result;
    }

    private static string JsonNodeToString(JsonNode node) =>
        node switch
        {
            JsonValue jsonValue => jsonValue.GetValueKind() switch
            {
                JsonValueKind.String => jsonValue.GetValue<string>() ?? string.Empty,
                JsonValueKind.Number => jsonValue.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => jsonValue.ToString()
            },
            _ => node.ToJsonString()
        };
}
