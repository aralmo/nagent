using System.Text.Json.Nodes;

namespace CustomAgents.Core.Tools;

public sealed class FileWriteTool : ITool
{
    public string Name => "file-write";
    public string Description => "Writes content to a file, creating parent directories if needed.";

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
            ["content"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Content to write."
            }
        },
        ["required"] = new JsonArray("path", "content")
    };

    public Task<string> InvokeAsync(JsonObject arguments, string workingPath, CancellationToken cancellationToken = default)
    {
        var path = arguments["path"]?.GetValue<string>()
            ?? throw new ArgumentException("file-write requires 'path'.");
        var content = arguments["content"]?.GetValue<string>() ?? string.Empty;
        var resolved = PathResolver.Resolve(path, workingPath);
        var directory = Path.GetDirectoryName(resolved);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(resolved, content);
        return Task.FromResult($"Wrote {content.Length} characters to {resolved}");
    }
}
