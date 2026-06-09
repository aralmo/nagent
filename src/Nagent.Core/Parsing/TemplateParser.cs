using System.Text.RegularExpressions;

namespace Nagent.Core.Parsing;

public sealed partial class TemplateParser
{
    [GeneratedRegex(@"\[(\w+):([^\]]*)\]", RegexOptions.Compiled)]
    private static partial Regex DirectiveRegex();

    [GeneratedRegex(@"<!--[\s\S]*?-->", RegexOptions.Compiled)]
    private static partial Regex HtmlCommentRegex();

    private readonly HashSet<string> _expandedPartials = new(StringComparer.OrdinalIgnoreCase);

    public ParsedTemplate ParseFile(string filePath, string workingPath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var content = ExpandPartials(File.ReadAllText(fullPath), workingPath, fullPath);
        return ParseContent(content);
    }

    private string ExpandPartials(string content, string workingPath, string sourcePath)
    {
        var nodes = new List<TemplateNode>();
        var offset = 0;

        while (offset < content.Length)
        {
            var match = DirectiveRegex().Match(content, offset);
            if (!match.Success)
            {
                break;
            }

            if (!match.Groups[1].Value.Equals("partial", StringComparison.OrdinalIgnoreCase))
            {
                offset = match.Index + match.Length;
                continue;
            }

            var partialPath = match.Groups[2].Value.Trim();
            var resolved = ResolvePath(partialPath, workingPath, sourcePath);
            if (!_expandedPartials.Add(resolved))
            {
                throw new InvalidOperationException($"Circular partial reference detected: {resolved}");
            }

            var partialContent = ExpandPartials(File.ReadAllText(resolved), workingPath, resolved);
            _expandedPartials.Remove(resolved);

            content = content[..match.Index] + partialContent + content[(match.Index + match.Length)..];
            offset = match.Index + partialContent.Length;
        }

        return content;
    }

    public ParsedTemplate ParseContent(string content)
    {
        var nodes = new List<TemplateNode>();
        var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lines = StripHtmlComments(content).Replace("\r\n", "\n").Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            ParseLine(line, lines, ref lineIndex, nodes, labels);
        }

        return new ParsedTemplate
        {
            Nodes = nodes,
            Labels = labels
        };
    }

    private void ParseLine(
        string line,
        string[] lines,
        ref int lineIndex,
        List<TemplateNode> nodes,
        Dictionary<string, int> labels)
    {
        var index = 0;

        while (index < line.Length)
        {
            var match = DirectiveRegex().Match(line, index);
            if (!match.Success)
            {
                var remaining = line[index..];
                if (!string.IsNullOrEmpty(remaining))
                {
                    nodes.Add(new TextNode(remaining));
                }

                break;
            }

            if (match.Index > index)
            {
                nodes.Add(new TextNode(line[index..match.Index]));
            }

            var tag = match.Groups[1].Value;
            var value = match.Groups[2].Value;

            switch (tag.ToLowerInvariant())
            {
                case "model":
                    nodes.Add(new ModelNode(value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
                    break;

                case "label":
                    labels[value.Trim()] = nodes.Count;
                    nodes.Add(new LabelNode(value.Trim()));
                    break;

                case "role":
                    nodes.Add(new RoleNode(value.Trim()));
                    break;

                case "do":
                    if (value.TrimStart().StartsWith("prompt_yesno(", StringComparison.OrdinalIgnoreCase))
                    {
                        var doCommand = ExtractPromptYesNoCommand(value, lines, ref lineIndex, line, match.Index + match.Length);
                        nodes.Add(new DoNode(doCommand));
                        return;
                    }

                    nodes.Add(new DoNode(value.Trim()));
                    break;

                case "tools":
                    nodes.Add(new ToolsNode(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
                    break;

                case "choose":
                    nodes.Add(new ChooseNode(value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
                    break;

                case "shell":
                    var shellCommand = ExtractShellCommand(value, lines, ref lineIndex, line, match.Index + match.Length);
                    nodes.Add(new ShellNode(shellCommand));
                    return;

                case "goto":
                    nodes.Add(new GotoNode(value.Trim()));
                    break;

                case "partial":
                    break;

                default:
                    nodes.Add(new TextNode(match.Value));
                    break;
            }

            index = match.Index + match.Length;
        }
    }

    private static string ExtractPromptYesNoCommand(
        string value,
        string[] lines,
        ref int lineIndex,
        string currentLine,
        int afterDirectiveIndex)
    {
        const string prefix = "prompt_yesno(";
        if (!value.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Expected prompt_yesno(...) command.");
        }

        var fenceStart = value.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart < 0)
        {
            throw new InvalidOperationException("prompt_yesno requires a ``` fenced message.");
        }

        var afterOpen = value[(fenceStart + 3)..];
        var closeInValue = afterOpen.IndexOf("```", StringComparison.Ordinal);
        if (closeInValue >= 0)
        {
            var message = afterOpen[..closeInValue];
            var afterClose = afterOpen[(closeInValue + 3)..].Trim();
            if (!afterClose.EndsWith(')'))
            {
                throw new InvalidOperationException("prompt_yesno command must end with ')' after label options.");
            }

            var labels = afterClose[..^1];
            ValidatePromptYesNoLabels(labels);
            return $"prompt_yesno(```{message}```{labels})";
        }

        var messageBuilder = new System.Text.StringBuilder();
        messageBuilder.Append(afterOpen.Trim());

        while (lineIndex + 1 < lines.Length)
        {
            lineIndex++;
            var nextLine = lines[lineIndex];
            var closeIndex = nextLine.IndexOf("```", StringComparison.Ordinal);
            if (closeIndex >= 0)
            {
                if (closeIndex > 0)
                {
                    messageBuilder.AppendLine();
                    messageBuilder.Append(nextLine[..closeIndex].TrimEnd());
                }

                var afterClose = nextLine[(closeIndex + 3)..].Trim();
                afterClose = StripDirectiveCloseBracket(afterClose);
                if (!afterClose.EndsWith(')'))
                {
                    throw new InvalidOperationException("prompt_yesno command must end with ')' after label options.");
                }

                var labels = afterClose[..^1];
                ValidatePromptYesNoLabels(labels);
                return $"prompt_yesno(```{messageBuilder}```{labels})";
            }

            messageBuilder.AppendLine();
            messageBuilder.Append(nextLine);
        }

        throw new InvalidOperationException("Unclosed ``` fence in prompt_yesno command.");
    }

    private static string StripDirectiveCloseBracket(string value)
    {
        if (value.EndsWith(']'))
        {
            return value[..^1].Trim();
        }

        return value;
    }

    private static void ValidatePromptYesNoLabels(string labels)
    {
        var parts = labels.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new InvalidOperationException("prompt_yesno requires two label options separated by '|'.");
        }
    }

    private static string ExtractShellCommand(
        string value,
        string[] lines,
        ref int lineIndex,
        string currentLine,
        int afterDirectiveIndex)
    {
        var fenceStart = value.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            var afterOpen = value[(fenceStart + 3)..];
            var closeInValue = afterOpen.IndexOf("```", StringComparison.Ordinal);
            if (closeInValue >= 0)
            {
                return afterOpen[..closeInValue].Trim();
            }

            var builder = new System.Text.StringBuilder();
            builder.Append(afterOpen.Trim());

            while (lineIndex + 1 < lines.Length)
            {
                lineIndex++;
                var nextLine = lines[lineIndex];
                var closeIndex = nextLine.IndexOf("```", StringComparison.Ordinal);
                if (closeIndex >= 0)
                {
                    if (closeIndex > 0)
                    {
                        builder.AppendLine();
                        builder.Append(nextLine[..closeIndex].TrimEnd());
                    }

                    return builder.ToString().Trim();
                }

                builder.AppendLine();
                builder.Append(nextLine);
            }

            return builder.ToString().Trim();
        }

        if (afterDirectiveIndex < currentLine.Length)
        {
            return currentLine[afterDirectiveIndex..].Trim();
        }

        return value.Trim();
    }

    private static string StripHtmlComments(string content) =>
        HtmlCommentRegex().Replace(content, string.Empty);

    private static string ResolvePath(string path, string workingPath, string sourcePath)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        var sourceDir = Path.GetDirectoryName(sourcePath) ?? workingPath;
        var candidate = Path.GetFullPath(Path.Combine(sourceDir, path));
        if (File.Exists(candidate))
        {
            return candidate;
        }

        return Path.GetFullPath(Path.Combine(workingPath, path));
    }
}
