using Nagent.Core.Domain;
using Nagent.Core.Hosting;
using Nagent.Core.Logging;
using Nagent.Core.Parsing;
using Nagent.Core.Persistence;
using Nagent.Core.Tools;

namespace Nagent.Core.Execution;

public sealed class AgentHandoverCoordinator(
    TemplateParser parser,
    IConversationLogger logger,
    IAgentHost host,
    SessionCheckpointService? checkpointService = null)
{
    private AgentEngine? _engine;

    public void Bind(AgentEngine engine) => _engine = engine;

    public async Task<string> HandoverAsync(
        AgentContext context,
        IReadOnlyList<ChatMessage> conversationSnapshot,
        string agentPath,
        string? prompt,
        CancellationToken cancellationToken = default)
    {
        if (_engine is null)
        {
            throw new InvalidOperationException("AgentHandoverCoordinator is not bound to an AgentEngine.");
        }

        string resolvedPath;
        try
        {
            resolvedPath = PathResolver.ResolveAgent(agentPath, context.WorkingPath);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }

        if (!File.Exists(resolvedPath))
        {
            return $"Error: agent template not found: {resolvedPath}";
        }

        await logger.LogAsync(new
        {
            type = "conversation_end",
            timestamp = DateTimeOffset.UtcNow,
            reason = "agent-handover",
            handover_target = agentPath,
            messages = conversationSnapshot.Select(m => new
            {
                role = m.Role.ToApiString(),
                content = m.Content,
                tool_calls = m.ToolCalls?.Select(t => new { t.Id, t.Name, t.ArgumentsJson }),
                tool_call_id = m.ToolCallId,
                name = m.Name
            })
        }, cancellationToken);

        await logger.RotateSessionAsync(context.WorkingPath, cancellationToken);

        await logger.LogAsync(new
        {
            type = "conversation_start",
            timestamp = DateTimeOffset.UtcNow,
            agent = agentPath,
            prompt
        }, cancellationToken);

        ResetContextForHandover(context, prompt, resolvedPath);

        if (checkpointService is not null)
        {
            await checkpointService.SaveAsync(context, cancellationToken: cancellationToken);
        }

        await host.WriteSystemMessageAsync($"Handed over to {agentPath}", cancellationToken);

        var template = parser.ParseFile(resolvedPath, context.WorkingPath);
        await _engine.RunAsync(template, context, initializeVariables: false, cancellationToken);

        context.HandoverPerformed = true;
        context.ProgramCounter = int.MaxValue;

        return $"Handed over to {resolvedPath}";
    }

    private static void ResetContextForHandover(AgentContext context, string? prompt, string templatePath)
    {
        context.History.Clear();
        context.CurrentBuffer = string.Empty;
        context.CurrentRole = null;
        context.ActiveToolNames.Clear();
        context.ActiveToolEntries.Clear();
        context.ModelFallbacks.Clear();
        context.InitialHistoryDisplayed = false;
        context.HandoverPerformed = false;
        context.ProgramCounter = 0;
        context.TemplatePath = templatePath;
        context.InitializeVariables();

        if (!string.IsNullOrEmpty(prompt))
        {
            context.Variables["prompt"] = prompt;
        }
    }
}
