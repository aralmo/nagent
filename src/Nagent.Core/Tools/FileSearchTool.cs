using System.Text.Json.Nodes;

namespace Nagent.Core.Tools;

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
                ["description"] = "File name filter, e.g. *.cs or **/*.py for recursive search"
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

        var (searchFolder, filePattern, searchRecurse) = NormalizeFilter(resolved, filter, recurse);

        if (!Directory.Exists(searchFolder))
        {
            return Task.FromResult($"Error: folder not found: {searchFolder}");
        }

        try
        {
            var option = searchRecurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(searchFolder, filePattern, option);
            return Task.FromResult(string.Join(Environment.NewLine, files));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts glob-style filters (e.g. **/*.py, tools/*.cs) into Directory.GetFiles parameters.
    /// </summary>
    private static (string searchFolder, string filePattern, bool recurse) NormalizeFilter(
        string folder, string filter, bool explicitRecurse)
    {
        var normalized = filter.Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');

        if (lastSlash < 0)
        {
            return (folder, filter, explicitRecurse);
        }

        var pathPart = normalized[..lastSlash];
        var filePart = normalized[(lastSlash + 1)..];
        if (string.IsNullOrEmpty(filePart))
        {
            filePart = "*";
        }

        var searchRecurse = explicitRecurse || pathPart.Contains("**", StringComparison.Ordinal);
        if (searchRecurse)
        {
            pathPart = pathPart
                .Replace("/**/", "/", StringComparison.Ordinal)
                .Replace("/**", "", StringComparison.Ordinal)
                .Replace("**/", "", StringComparison.Ordinal)
                .Replace("**", "", StringComparison.Ordinal);
        }

        var searchFolder = string.IsNullOrEmpty(pathPart)
            ? folder
            : Path.Combine(folder, pathPart.Replace('/', Path.DirectorySeparatorChar));

        return (searchFolder, filePart, searchRecurse);
    }
}
