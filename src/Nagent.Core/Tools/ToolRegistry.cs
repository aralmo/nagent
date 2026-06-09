using Nagent.Core.Domain;
using Nagent.Core.Providers;
using Nagent.Core.Shell;

namespace Nagent.Core.Tools;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _toolsByFile = new(StringComparer.OrdinalIgnoreCase);
    private readonly ShellRunner _shellRunner;

    public ToolRegistry(IEnumerable<ITool> tools, ShellRunner shellRunner)
    {
        _shellRunner = shellRunner;
        foreach (var tool in tools)
        {
            Register(tool);
        }
    }

    public void Register(ITool tool)
    {
        if (_tools.ContainsKey(tool.Name))
        {
            throw new InvalidOperationException($"Tool '{tool.Name}' is already registered.");
        }

        _tools[tool.Name] = tool;
    }

    public void RegisterAll(IEnumerable<ITool> tools)
    {
        foreach (var tool in tools)
        {
            Register(tool);
        }
    }

    public void LoadFromFile(string path) =>
        ReloadFromFile(Path.GetFullPath(path));

    public IReadOnlyList<string> GetToolNamesFromFile(string path, string workingPath, string? templatePath)
    {
        var resolvedPath = PathResolver.ResolveRelativeFile(path, workingPath, templatePath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException($"Custom tools file not found: {resolvedPath}");
        }

        ReloadFromFile(resolvedPath);
        return CustomToolLoader.ReadNamesFromFile(resolvedPath);
    }

    public IReadOnlyList<string> ResolveActiveToolNames(
        IReadOnlyList<string> entries,
        IReadOnlyList<string> fallbackNames,
        string workingPath,
        string? templatePath) =>
        entries.Count > 0
            ? ExpandToolNames(entries, workingPath, templatePath)
            : fallbackNames;

    private void ReloadFromFile(string resolvedPath)
    {
        var customTools = CustomToolLoader.LoadFromFile(resolvedPath, _shellRunner);

        if (_toolsByFile.TryGetValue(resolvedPath, out var previousNames))
        {
            foreach (var name in previousNames)
            {
                if (_tools.TryGetValue(name, out var existing) && existing is ShellCommandTool)
                {
                    _tools.Remove(name);
                }
            }
        }

        var newNames = new List<string>();
        foreach (var tool in customTools)
        {
            if (_tools.TryGetValue(tool.Name, out var existing) && existing is not ShellCommandTool)
            {
                throw new InvalidOperationException(
                    $"Custom tool '{tool.Name}' in '{resolvedPath}' conflicts with a built-in tool.");
            }

            _tools[tool.Name] = tool;
            newNames.Add(tool.Name);
        }

        _toolsByFile[resolvedPath] = newNames;
    }

    public IReadOnlyList<string> ExpandToolNames(
        IEnumerable<string> entries,
        string workingPath,
        string? templatePath)
    {
        var names = new List<string>();

        foreach (var entry in entries)
        {
            if (ToolFileReference.TryParse(entry, out var filePath))
            {
                names.AddRange(GetToolNamesFromFile(filePath, workingPath, templatePath));
                continue;
            }

            names.Add(entry);
        }

        return names;
    }

    public IReadOnlyList<ToolSchema> GetSchemas(IEnumerable<string> names)
    {
        var schemas = new List<ToolSchema>();
        foreach (var name in names)
        {
            if (_tools.TryGetValue(name, out var tool))
            {
                schemas.Add(tool.ToSchema());
            }
            else
            {
                Console.Error.WriteLine($"Warning: tool '{name}' is not registered.");
            }
        }

        return schemas;
    }

    public async Task<string> InvokeAsync(
        string name,
        string argumentsJson,
        string workingPath,
        AgentContext? context = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_tools.TryGetValue(name, out var tool))
            {
                return $"Error: unknown tool '{name}'.";
            }

            if (tool is ShellTool shellTool)
            {
                return await shellTool.InvokeFromRawAsync(argumentsJson, workingPath, cancellationToken);
            }

            var args = System.Text.Json.Nodes.JsonNode.Parse(argumentsJson)?.AsObject()
                ?? new System.Text.Json.Nodes.JsonObject();

            if (tool is ShellCommandTool shellCommandTool)
            {
                return await shellCommandTool.InvokeAsync(args, workingPath, context, cancellationToken);
            }

            return await tool.InvokeAsync(args, workingPath, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
