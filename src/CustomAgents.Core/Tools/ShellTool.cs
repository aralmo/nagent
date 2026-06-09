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
            },
            ["timeout"] = new JsonObject
            {
                ["type"] = "number",
                ["description"] = "Maximum seconds to wait for the command. Defaults to 10."
            }
        },
        ["required"] = new JsonArray("command")
    };

    public async Task<string> InvokeAsync(JsonObject arguments, string workingPath, CancellationToken cancellationToken = default)
    {
        var command = ShellCommandParser.Parse(arguments);
        var timeout = ShellCommandParser.ParseTimeout(arguments);
        var result = await shellRunner.RunAsync(command, workingPath, timeout, cancellationToken);
        return FormatResult(result);
    }

    public async Task<string> InvokeFromRawAsync(
        string argumentsJson,
        string workingPath,
        CancellationToken cancellationToken = default)
    {
        var command = ShellCommandParser.Parse(argumentsJson);
        var timeout = ShellCommandParser.ParseTimeout(argumentsJson);
        var result = await shellRunner.RunAsync(command, workingPath, timeout, cancellationToken);
        return FormatResult(result);
    }

    private static string FormatResult(ShellRunResult result)
    {
        if (!result.TimedOut)
        {
            return result.Output;
        }

        return string.IsNullOrEmpty(result.Output)
            ? "tool timed out"
            : $"tool timed out\n\n{result.Output}";
    }
}
