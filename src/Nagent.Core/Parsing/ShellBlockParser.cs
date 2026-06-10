using System.Text;
using System.Text.RegularExpressions;

namespace Nagent.Core.Parsing;

public sealed record ShellBlock(string Command, bool Silent);

public sealed class ShellBlockParseResult
{
    public required string StrippedContent { get; init; }
    public required IReadOnlyList<ShellBlock> Blocks { get; init; }
}

public static partial class ShellBlockParser
{
    [GeneratedRegex(@"```(shell-silent|shell)\s*\r?\n([\s\S]*?)```", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ShellFenceRegex();

    public static ShellBlockParseResult Parse(string content)
    {
        var blocks = new List<ShellBlock>();
        var stripped = ShellFenceRegex().Replace(content, match =>
        {
            var tag = match.Groups[1].Value;
            var command = match.Groups[2].Value.Trim();
            var silent = tag.Equals("shell-silent", StringComparison.OrdinalIgnoreCase);
            blocks.Add(new ShellBlock(command, silent));
            return string.Empty;
        });

        return new ShellBlockParseResult
        {
            StrippedContent = CollapseExtraBlankLines(stripped),
            Blocks = blocks
        };
    }

    private static string CollapseExtraBlankLines(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var builder = new StringBuilder();
        var previousBlank = false;

        foreach (var line in lines)
        {
            var isBlank = string.IsNullOrWhiteSpace(line);
            if (isBlank && previousBlank)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(line.TrimEnd());
            previousBlank = isBlank;
        }

        return builder.ToString().TrimEnd();
    }
}
