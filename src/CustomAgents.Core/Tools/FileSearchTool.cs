using System.Text.Json.Nodes;

namespace CustomAgents.Core.Tools;

public sealed class FileSearchTool : ITool
{
    public string Name => "file-search";
    public string Description => "Searches for files in a folder using an optional filter pattern.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["folder"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Folder to search in."
            },
            ["filter"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "File name filter, e.g. *.cs"
            },
            ["recurse"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] = "Whether to search recursively."
            }
        },
        ["required"] = new JsonArray("folder")
    };

    public Task<string> InvokeAsync(JsonObject arguments, string workingPath, CancellationToken cancellationToken = default)
    {
        var folder = arguments["folder"]?.GetValue<string>()
            ?? throw new ArgumentException("file-search requires 'folder'.");
        var filter = arguments["filter"]?.GetValue<string>() ?? "*";
        var recurse = arguments["recurse"]?.GetValue<bool>() ?? false;
        var resolved = PathResolver.Resolve(folder, workingPath);

        if (!Directory.Exists(resolved))
        {
            return Task.FromResult($"Error: folder not found: {resolved}");
        }

        var option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(resolved, filter, option);
        return Task.FromResult(string.Join(Environment.NewLine, files));
    }
}
