namespace CustomAgents.Core.Domain;

public sealed record ModelRef(string Provider, string ModelName)
{
    public static ModelRef Parse(string value)
    {
        var trimmed = value.Trim();
        var at = trimmed.IndexOf('@');
        if (at <= 0 || at >= trimmed.Length - 1)
        {
            throw new FormatException($"Invalid model reference '{value}'. Expected provider@modelname.");
        }

        return new ModelRef(trimmed[..at], trimmed[(at + 1)..]);
    }

    public override string ToString() => $"{Provider}@{ModelName}";
}
