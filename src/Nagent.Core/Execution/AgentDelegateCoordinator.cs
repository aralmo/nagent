using Nagent.Core.Domain;
using Nagent.Core.Hosting;
using Nagent.Core.Logging;
using Nagent.Core.Parsing;
using Nagent.Core.Persistence;
using Nagent.Core.Tools;

namespace Nagent.Core.Execution;

public sealed class AgentDelegateCoordinator(
    TemplateParser parser,
    IConversationLogger logger,
    IAgentHost host,
    SessionCheckpointService? checkpointService = null)
{
    private AgentEngine? _engine;

    public void Bind(AgentEngine engine) => _engine = engine;

    public async Task<string> DelegateAsync(
        AgentContext parentContext,
        string agentPath,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (_engine is null)
        {
            throw new InvalidOperationException("AgentDelegateCoordinator is not bound to an AgentEngine.");
        }

        string resolvedPath;
        try
        {
            resolvedPath = PathResolver.ResolveAgent(agentPath, parentContext.WorkingPath);
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
            type = "delegate_start",
            timestamp = DateTimeOffset.UtcNow,
            agent = agentPath,
            prompt
        }, cancellationToken);

        await logger.RotateSessionAsync(parentContext.WorkingPath, cancellationToken);

        await logger.LogAsync(new
        {
            type = "conversation_start",
            timestamp = DateTimeOffset.UtcNow,
            agent = agentPath,
            prompt
        }, cancellationToken);

        if (checkpointService is not null)
        {
            await checkpointService.SaveAsync(parentContext, cancellationToken: cancellationToken);
        }

        var childContext = CreateChildContext(parentContext, prompt, resolvedPath);

        await host.WriteSystemMessageAsync($"Delegating to {agentPath}", cancellationToken);

        var template = parser.ParseFile(resolvedPath, parentContext.WorkingPath);
        await Task.Run(
            () => _engine.RunAsync(template, childContext, initializeVariables: true, cancellationToken),
            cancellationToken);

        var completion = childContext.Variables.TryGetValue("completion", out var value)
            ? value
            : string.Empty;

        await logger.LogAsync(new
        {
            type = "delegate_end",
            timestamp = DateTimeOffset.UtcNow,
            agent = agentPath,
            completion
        }, cancellationToken);

        await logger.RotateSessionAsync(parentContext.WorkingPath, cancellationToken);

        if (checkpointService is not null)
        {
            await checkpointService.SaveAsync(parentContext, cancellationToken: cancellationToken);
        }

        return completion;
    }

    private static AgentContext CreateChildContext(AgentContext parentContext, string prompt, string templatePath)
    {
        var childContext = new AgentContext
        {
            WorkingPath = parentContext.WorkingPath,
            TemplatePath = templatePath,
            InitialPrompt = prompt,
            SuppressOutput = parentContext.SuppressOutput
        };
        childContext.InitializeVariables();
        childContext.Variables["prompt"] = prompt;

        return childContext;
    }
}
