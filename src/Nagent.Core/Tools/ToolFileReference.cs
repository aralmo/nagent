using System.Text.RegularExpressions;

namespace Nagent.Core.Tools;

public static partial class ToolFileReference
{
    [GeneratedRegex(@"^file\((['""])(.+)\1\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileReferenceRegex();

    public static bool TryParse(string entry, out string path)
    {
        var match = FileReferenceRegex().Match(entry.Trim());
        if (!match.Success)
        {
            path = string.Empty;
            return false;
        }

        path = match.Groups[2].Value;
        return true;
    }
}
