using System.Text.RegularExpressions;
using Nagent.Core.Domain;

namespace Nagent.Core.Variables;

public static partial class VariableSubstitution
{
    [GeneratedRegex(@"\{\$(\w+)\}")]
    private static partial Regex BraceVariableRegex();

    [GeneratedRegex(@"(?<!\{)\$(\w+)(?!\})")]
    private static partial Regex BareVariableRegex();

    public static string Substitute(string input, AgentContext context)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = BraceVariableRegex().Replace(input, match =>
        {
            var key = match.Groups[1].Value;
            return context.Variables.TryGetValue(key, out var value) ? value : match.Value;
        });

        result = BareVariableRegex().Replace(result, match =>
        {
            var key = match.Groups[1].Value;
            return context.Variables.TryGetValue(key, out var value) ? value : match.Value;
        });

        return result;
    }

    public static string SerializeHistory(IReadOnlyList<ChatMessage> history)
    {
        if (history.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            history.Select(m =>
            {
                var prefix = $"[{m.Role.ToApiString().ToUpperInvariant()}]";
                if (m.ToolCalls is { Count: > 0 })
                {
                    var calls = string.Join(", ", m.ToolCalls.Select(t => $"{t.Name}({t.ArgumentsJson})"));
                    return $"{prefix} {m.Content} [tool_calls: {calls}]";
                }

                if (m.Role == ChatRole.Tool)
                {
                    return $"{prefix} ({m.Name}) {m.Content}";
                }

                return $"{prefix} {m.Content}";
            }));
    }
}
