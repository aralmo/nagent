namespace CustomAgents.Core.Shell;

public static class ShellQuoter
{
    public static string Quote(string value)
    {
        if (OperatingSystem.IsWindows())
        {
            return QuoteForCmd(value);
        }

        return QuoteForBash(value);
    }

    private static string QuoteForCmd(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string QuoteForBash(string value)
    {
        if (!value.Contains('\'', StringComparison.Ordinal))
        {
            return "'" + value + "'";
        }

        return "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }
}
