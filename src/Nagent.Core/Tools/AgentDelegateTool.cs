using System.Text.Json.Nodes;

namespace Nagent.Core.Tools;

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
                ["description"] = "Agent template filename or path. Bare filenames are resolved from the working directory, then from workingPath/.agents/."
            },
            ["prompt"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Prompt to set as $prompt on the delegated agent (does not skip do:prompt())."
            }
        },
        ["required"] = new JsonArray("agent", "prompt")
    };

    public Task<string> InvokeAsync(
        JsonObject arguments,
        string workingPath,
        CancellationToken cancellationToken = default) =>
        Task.FromResult("Error: agent-delegate must be invoked through the turn runner.");
}
