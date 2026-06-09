using System.Text.Json.Nodes;

namespace CustomAgents.Core.Tools;

public sealed class AgentDelegateTool : ITool
{
    public string Name => "agent-delegate";
    public string Description =>
        "Delegates to a new agent template in an isolated session. Waits for it to finish and returns the child's last completion. The current conversation continues.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["agent"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Relative path to the agent template .md file from the working directory."
            },
            ["prompt"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Optional prompt to set as $prompt on the delegated agent (does not skip do:prompt())."
            }
        },
        ["required"] = new JsonArray("agent")
    };

    public Task<string> InvokeAsync(
        JsonObject arguments,
        string workingPath,
        CancellationToken cancellationToken = default) =>
        Task.FromResult("Error: agent-delegate must be invoked through the turn runner.");
}
