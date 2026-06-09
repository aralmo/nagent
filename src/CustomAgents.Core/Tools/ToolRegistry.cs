using CustomAgents.Core.Domain;
using CustomAgents.Core.Providers;
using CustomAgents.Core.Shell;

namespace CustomAgents.Core.Tools;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ShellRunner _shellRunner;
    private readonly HashSet<string> _loadedFiles = new(StringComparer.OrdinalIgnoreCase);

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

    public void LoadFromFile(string path)
    {
        var resolvedPath = Path.GetFullPath(path);
        if (!_loadedFiles.Add(resolvedPath))
        {
            return;
        }

        var customTools = CustomToolLoader.LoadFromFile(resolvedPath, _shellRunner);
        RegisterAll(customTools);
    }

    public IReadOnlyList<string> GetToolNamesFromFile(string path, string workingPath, string? templatePath)
    {
        var resolvedPath = PathResolver.ResolveRelativeFile(path, workingPath, templatePath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException($"Custom tools file not found: {resolvedPath}");
        }

        LoadFromFile(resolvedPath);
        return CustomToolLoader.ReadNamesFromFile(resolvedPath);
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
}
