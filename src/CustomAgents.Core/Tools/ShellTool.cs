using System.Text.Json.Nodes;
using CustomAgents.Core.Shell;

namespace CustomAgents.Core.Tools;

public sealed class ShellTool(ShellRunner shellRunner) : ITool
{
    public string Name => "shell";
    public string Description => "Runs a shell command from the working directory and returns stdout/stderr.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["command"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Shell command to execute."
            }
        },
        ["required"] = new JsonArray("command")
    };

    public Task<string> InvokeAsync(JsonObject arguments, string workingPath, CancellationToken cancellationToken = default)
    {
        var command = ShellCommandParser.Parse(arguments);
        return shellRunner.RunAsync(command, workingPath, cancellationToken);
    }

    public Task<string> InvokeFromRawAsync(
        string argumentsJson,
        string workingPath,
        CancellationToken cancellationToken = default)
    {
        var command = ShellCommandParser.Parse(argumentsJson);
        return shellRunner.RunAsync(command, workingPath, cancellationToken);
    }
}
