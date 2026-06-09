using System.Text.Json.Serialization;

namespace CustomAgents.Core.Tools;

public sealed class CustomToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; init; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<CustomToolParameterDefinition> Parameters { get; init; } = [];
}

public sealed class CustomToolParameterDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}
