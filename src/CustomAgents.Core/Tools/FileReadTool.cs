using System.Text.Json.Nodes;

namespace CustomAgents.Core.Tools;

public sealed class FileReadTool : ITool
{
    public string Name => "file-read";
    public string Description => "Reads the content of a file. Optionally truncates to max_length characters.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Path to the file. Relative paths use the working directory. ~ is supported."
            },
            ["max_length"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "Maximum number of characters to return."
            }
        },
        ["required"] = new JsonArray("path")
    };

    public Task<string> InvokeAsync(JsonObject arguments, string workingPath, CancellationToken cancellationToken = default)
    {
        var path = arguments["path"]?.GetValue<string>()
            ?? throw new ArgumentException("file-read requires 'path'.");
        var resolved = PathResolver.Resolve(path, workingPath);

        if (!File.Exists(resolved))
        {
            return Task.FromResult($"Error: file not found: {resolved}");
        }

        var content = File.ReadAllText(resolved);
        if (arguments.TryGetPropertyValue("max_length", out var maxNode) &&
            maxNode is not null &&
            maxNode.GetValueKind() == System.Text.Json.JsonValueKind.Number)
        {
            var maxLength = maxNode.GetValue<int>();
            if (content.Length > maxLength)
            {
                content = content[..maxLength] + "\n...[truncated]";
            }
        }

        return Task.FromResult(content);
    }
}
