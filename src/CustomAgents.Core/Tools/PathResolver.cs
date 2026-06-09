namespace CustomAgents.Core.Tools;

internal static class PathResolver
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

        if (Path.IsPathRooted(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        return Path.GetFullPath(Path.Combine(workingPath, expanded));
    }
}
