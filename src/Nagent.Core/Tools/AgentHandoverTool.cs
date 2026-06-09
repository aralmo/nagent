using System.Text.Json.Nodes;

namespace Nagent.Core.Tools;

public sealed class AgentHandoverTool : ITool
{
    public string Name => "agent-handover";
    public string Description =>
        "Hands over to a new agent template. Ends the current conversation, clears history, and runs the specified agent.md.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["agent"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Agent template filename or path. Bare filenames are resolved from the working directory, then from workingPath/.agents/."
            },
            ["prompt"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Optional prompt to set as $prompt on the new agent (does not skip do:prompt())."
            }
        },
        ["required"] = new JsonArray("agent")
    };

    public Task<string> InvokeAsync(
        JsonObject arguments,
        string workingPath,
        CancellationToken cancellationToken = default) =>
        Task.FromResult("Error: agent-handover must be invoked through the turn runner.");
}
