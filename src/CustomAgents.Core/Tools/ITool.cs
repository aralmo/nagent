using System.Text.Json.Nodes;
using CustomAgents.Core.Providers;

namespace CustomAgents.Core.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonObject ParametersSchema { get; }
    Task<string> InvokeAsync(JsonObject arguments, string workingPath, CancellationToken cancellationToken = default);

    ToolSchema ToSchema() => new()
    {
        Name = Name,
        Description = Description,
        Parameters = ParametersSchema
    };
}
