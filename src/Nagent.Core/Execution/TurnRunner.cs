using Nagent.Core.Domain;
using Nagent.Core.Hosting;
using Nagent.Core.Logging;
using Nagent.Core.Providers;
using Nagent.Core.Tools;

namespace Nagent.Core.Execution;

public sealed class TurnRunner(
    ModelRequestService modelRequestService,
    ToolRegistry toolRegistry,
    ShellBlockExecutor shellBlockExecutor,
    IAgentHost host,
    IConversationLogger logger,
    AgentHandoverCoordinator handoverCoordinator,
    AgentDelegateCoordinator delegateCoordinator)
{
    public async Task<string> RunTurnAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ModelFallbacks.Count == 0)
        {
            throw new InvalidOperationException("No model fallbacks configured.");
        }

        var workingMessages = context.History.Select(m => m.Clone()).ToList();
        var turnStartCount = workingMessages.Count;

        while (true)
        {
            var activeToolNames = toolRegistry.ResolveActiveToolNames(
                context.ActiveToolEntries,
                context.ActiveToolNames,
                context.WorkingPath,
                context.TemplatePath);
            var tools = activeToolNames.Count > 0
                ? toolRegistry.GetSchemas(activeToolNames)
                : null;

            var result = await modelRequestService.CompleteAsync(
                context.ModelFallbacks,
                workingMessages,
                tools,
                context,
                updateCompletion: true,
                streamToHost: !context.SuppressOutput,
                cancellationToken: cancellationToken);

            if (result.ToolCalls.Count == 0)
            {
                var processed = await shellBlockExecutor.ProcessAsync(
                    result.Content,
                    context,
                    cancellationToken);
                var finalContent = processed.StrippedContent;
                context.Variables["completion"] = finalContent;

                workingMessages.Add(new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Content = finalContent
                });

                if (processed.InjectedUserMessage is not null)
                {
                    workingMessages.Add(new ChatMessage
                    {
                        Role = ChatRole.User,
                        Content = processed.InjectedUserMessage
                    });
                }

                AppendTurnMessages(context, workingMessages, turnStartCount);
                return finalContent;
            }

            workingMessages.Add(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = result.Content,
                ToolCalls = result.ToolCalls.ToList()
            });

            foreach (var toolCall in result.ToolCalls)
            {
                await logger.LogAsync(new
                {
                    type = "tool_call",
                    timestamp = DateTimeOffset.UtcNow,
                    id = toolCall.Id,
                    name = toolCall.Name,
                    arguments = toolCall.ArgumentsJson
                }, cancellationToken);

                var toolResult = await ExecuteToolCallAsync(
                    toolCall,
                    context,
                    workingMessages,
                    cancellationToken);

                await host.WriteToolResponseAsync(toolCall.Name, toolResult, cancellationToken);

                await logger.LogAsync(new
                {
                    type = "tool_response",
                    timestamp = DateTimeOffset.UtcNow,
                    id = toolCall.Id,
                    name = toolCall.Name,
                    content = toolResult
                }, cancellationToken);

                if (toolCall.Name.Equals("agent-handover", StringComparison.OrdinalIgnoreCase) &&
                    !IsToolError(toolResult))
                {
                    return toolResult;
                }

                workingMessages.Add(new ChatMessage
                {
                    Role = ChatRole.Tool,
                    Content = toolResult,
                    ToolCallId = toolCall.Id,
                    Name = toolCall.Name
                });
            }
        }
    }

    private async Task<string> ExecuteToolCallAsync(
        ToolCall toolCall,
        AgentContext context,
        List<ChatMessage> workingMessages,
        CancellationToken cancellationToken)
    {
        try
        {
            if (toolCall.Name.Equals("agent-handover", StringComparison.OrdinalIgnoreCase))
            {
                var (agentPath, prompt) = ParseAgentSubArguments(toolCall.ArgumentsJson);
                var snapshot = workingMessages.Select(m => m.Clone()).ToList();
                return await handoverCoordinator.HandoverAsync(
                    context,
                    snapshot,
                    agentPath,
                    prompt,
                    cancellationToken);
            }

            if (toolCall.Name.Equals("agent-delegate", StringComparison.OrdinalIgnoreCase))
            {
                var (agentPath, prompt) = ParseAgentSubArguments(toolCall.ArgumentsJson);
                return await delegateCoordinator.DelegateAsync(
                    context,
                    agentPath,
                    prompt,
                    cancellationToken);
            }

            return await toolRegistry.InvokeAsync(
                toolCall.Name,
                toolCall.ArgumentsJson,
                context.WorkingPath,
                context,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static bool IsToolError(string toolResult) =>
        toolResult.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);

    private static (string AgentPath, string? Prompt) ParseAgentSubArguments(string argumentsJson)
    {
        var args = System.Text.Json.Nodes.JsonNode.Parse(argumentsJson)?.AsObject()
            ?? throw new InvalidOperationException("agent tool requires JSON arguments.");

        var agentPath = args["agent"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(agentPath))
        {
            throw new InvalidOperationException("agent tool requires 'agent' parameter.");
        }

        var prompt = args["prompt"]?.GetValue<string>();
        return (agentPath, prompt);
    }

    private static void AppendTurnMessages(
        AgentContext context,
        List<ChatMessage> workingMessages,
        int turnStartCount)
    {
        for (var i = turnStartCount; i < workingMessages.Count; i++)
        {
            context.History.Add(workingMessages[i].Clone());
        }
    }
}
