using System.Text.Json;
using CustomAgents.Core.Shell;

namespace CustomAgents.Core.Tools;

public static class CustomToolLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<string> ReadNamesFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var definitions = JsonSerializer.Deserialize<List<CustomToolDefinition>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Custom tools file is empty or invalid: {path}");

        return definitions
            .Select(d => d.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
    }

    public static IReadOnlyList<ShellCommandTool> LoadFromFile(string path, ShellRunner shellRunner)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Custom tools file not found: {path}");
        }

        var json = File.ReadAllText(path);
        var definitions = JsonSerializer.Deserialize<List<CustomToolDefinition>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Custom tools file is empty or invalid: {path}");

        var tools = new List<ShellCommandTool>();
        foreach (var definition in definitions)
        {
            ValidateDefinition(definition, path);
            tools.Add(new ShellCommandTool(shellRunner, definition));
        }

        return tools;
    }

    private static void ValidateDefinition(CustomToolDefinition definition, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new InvalidOperationException($"Custom tool in '{sourcePath}' is missing 'name'.");
        }

        if (string.IsNullOrWhiteSpace(definition.Command))
        {
            throw new InvalidOperationException(
                $"Custom tool '{definition.Name}' in '{sourcePath}' is missing 'command'.");
        }

        if (BuiltInToolNames.Contains(definition.Name))
        {
            throw new InvalidOperationException(
                $"Custom tool '{definition.Name}' in '{sourcePath}' conflicts with a built-in tool.");
        }
    }

    public static IReadOnlySet<string> BuiltInToolNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "file-read",
        "file-write",
        "file-search",
        "shell",
        "agent-handover",
        "agent-delegate"
    };
}
