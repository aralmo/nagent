namespace Nagent.Core.Tools;

public static class PathResolver
{
    public static string ResolveRelativeFile(string path, string workingPath, string? templatePath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.");
        }

        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        if (!string.IsNullOrWhiteSpace(templatePath))
        {
            var templateDir = Path.GetDirectoryName(templatePath) ?? workingPath;
            var candidate = Path.GetFullPath(Path.Combine(templateDir, path));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(Path.Combine(workingPath, path));
    }

    public static string Resolve(string path, string workingPath)
    {
        var expanded = ExpandTilde(path);

        if (Path.IsPathRooted(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        return Path.GetFullPath(Path.Combine(workingPath, expanded));
    }

    public static string ResolveAgent(string path, string workingPath)
    {
        var expanded = ExpandTilde(path);

        if (Path.IsPathRooted(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        if (HasDirectoryComponent(expanded))
        {
            return Path.GetFullPath(Path.Combine(workingPath, expanded));
        }

        var direct = Path.GetFullPath(Path.Combine(workingPath, expanded));
        if (File.Exists(direct))
        {
            return direct;
        }

        var inAgents = Path.GetFullPath(Path.Combine(workingPath, ".agents", expanded));
        if (File.Exists(inAgents))
        {
            return inAgents;
        }

        return direct;
    }

    private static string ExpandTilde(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.");
        }

        var expanded = path.Trim();
        if (expanded.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = Path.Combine(home, expanded.TrimStart('~').TrimStart('/', '\\'));
        }

        return expanded;
    }

    private static bool HasDirectoryComponent(string path) =>
        path.Contains('/') || path.Contains('\\');
}
