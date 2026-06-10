using System.Text;
using Nagent.Core.Domain;
using Nagent.Core.Hosting;
using Nagent.Core.Logging;
using Nagent.Core.Parsing;
using Nagent.Core.Shell;
using Nagent.Core.Variables;

namespace Nagent.Core.Execution;

public sealed class ShellBlockExecutor(
    ShellRunner shellRunner,
    IAgentHost host,
    IConversationLogger logger)
{
    public async Task<ShellBlockProcessResult> ProcessAsync(
        string content,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var parsed = ShellBlockParser.Parse(content);
        if (parsed.Blocks.Count == 0)
        {
            return new ShellBlockProcessResult(content, null);
        }

        var outputBuilder = new StringBuilder();

        foreach (var block in parsed.Blocks)
        {
            if (string.IsNullOrWhiteSpace(block.Command))
            {
                continue;
            }

            var command = VariableSubstitution.Substitute(block.Command, context);
            var result = await shellRunner.RunAsync(command, context.WorkingPath, cancellationToken);

            await logger.LogAsync(new
            {
                type = "shell_block",
                timestamp = DateTimeOffset.UtcNow,
                silent = block.Silent,
                command,
                output = result.Output
            }, cancellationToken);

            await host.WriteShellBlockAsync(command, result.Output, block.Silent, cancellationToken);

            if (!block.Silent)
            {
                if (outputBuilder.Length > 0)
                {
                    outputBuilder.AppendLine();
                    outputBuilder.AppendLine();
                }

                outputBuilder.Append(result.Output);
            }
        }

        string? injectedUserMessage = outputBuilder.Length > 0
            ? "[shell output]\n" + outputBuilder
            : null;

        return new ShellBlockProcessResult(parsed.StrippedContent, injectedUserMessage);
    }
}

public sealed record ShellBlockProcessResult(string StrippedContent, string? InjectedUserMessage);
