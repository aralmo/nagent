using CustomAgents.Core.Domain;
using CustomAgents.Core.Hosting;
using CustomAgents.Core.Parsing;
using CustomAgents.Core.Providers;
using CustomAgents.Core.Shell;
using CustomAgents.Core.Tools;
using CustomAgents.Core.Variables;

namespace CustomAgents.Core.Execution;

public sealed class AgentEngine(
    IAgentHost host,
    TurnRunner turnRunner,
    ModelRequestService modelRequestService,
    ShellRunner shellRunner,
    ToolRegistry toolRegistry)
{
    public async Task RunAsync(
        ParsedTemplate template,
        AgentContext context,
        bool initializeVariables = true,
        CancellationToken cancellationToken = default)
    {
        if (initializeVariables)
        {
            context.InitializeVariables();
        }

        while (context.ProgramCounter < template.Nodes.Count)
        {
            if (context.HandoverPerformed)
            {
                return;
            }

            var node = template.Nodes[context.ProgramCounter];
            context.ProgramCounter++;

            switch (node)
            {
                case TextNode textNode:
                    context.CurrentBuffer += VariableSubstitution.Substitute(textNode.Text, context);
                    break;

                case ModelNode modelNode:
                    context.ModelFallbacks = modelNode.Models.Select(ModelRef.Parse).ToList();
                    break;

                case LabelNode:
                    break;

                case RoleNode roleNode:
                    FlushBuffer(context);
                    context.CurrentRole = ChatRoleExtensions.FromTag(roleNode.Role);
                    break;

                case DoNode doNode:
                    await ExecuteDoAsync(template, doNode, context, cancellationToken);
                    break;

                case ToolsNode toolsNode:
                    context.ActiveToolNames = toolRegistry
                        .ExpandToolNames(toolsNode.Tools, context.WorkingPath, context.TemplatePath)
                        .ToList();
                    break;

                case ShellNode shellNode:
                    context.RefreshDateTime();
                    var command = VariableSubstitution.Substitute(shellNode.Command, context);
                    var output = await shellRunner.RunAsync(command, context.WorkingPath, cancellationToken);
                    context.CurrentBuffer += output;
                    break;

                case GotoNode gotoNode:
                    JumpToLabel(template, context, gotoNode.Label);
                    break;

                case ChooseNode chooseNode:
                    var jumped = await ExecuteChooseAsync(template, chooseNode, context, cancellationToken);
                    if (!jumped)
                    {
                        return;
                    }

                    break;
            }
        }

        FlushBuffer(context);
    }

    private async Task ExecuteDoAsync(
        ParsedTemplate template,
        DoNode node,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        var command = node.Command.Trim();
        if (command.Equals("prompt()", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayInitialHistoryAsync(context, cancellationToken);

            string? prompt;
            if (!context.InitialPromptConsumed && !string.IsNullOrWhiteSpace(context.InitialPrompt))
            {
                prompt = context.InitialPrompt;
                context.InitialPromptConsumed = true;
            }
            else
            {
                prompt = await host.PromptUserAsync(cancellationToken);
            }

            if (prompt is null)
            {
                context.ProgramCounter = int.MaxValue;
                return;
            }

            context.Variables["prompt"] = prompt;
            context.CurrentBuffer += prompt;
            return;
        }

        if (command.StartsWith("prompt_yesno(", StringComparison.OrdinalIgnoreCase))
        {
            var (message, yesLabel, noLabel) = ParsePromptYesNo(command);
            message = VariableSubstitution.Substitute(message, context);
            await DisplayInitialHistoryAsync(context, cancellationToken);

            var selected = await host.PromptChoiceAsync(message, yesLabel, noLabel, cancellationToken);
            if (selected is null)
            {
                context.ProgramCounter = int.MaxValue;
                return;
            }

            JumpToLabel(template, context, selected);
            return;
        }

        if (command.Equals("turn()", StringComparison.OrdinalIgnoreCase))
        {
            FlushBuffer(context);
            var completion = await turnRunner.RunTurnAsync(context, cancellationToken);
            if (context.HandoverPerformed)
            {
                context.ProgramCounter = int.MaxValue;
                return;
            }

            context.Variables["completion"] = completion;
            return;
        }

        throw new InvalidOperationException($"Unknown do command: {node.Command}");
    }

    private static (string Message, string YesLabel, string NoLabel) ParsePromptYesNo(string command)
    {
        const string prefix = "prompt_yesno(";
        if (!command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !command.EndsWith(')'))
        {
            throw new InvalidOperationException($"Malformed prompt_yesno command: {command}");
        }

        var inner = command[prefix.Length..^1];
        var fenceStart = inner.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart < 0)
        {
            throw new InvalidOperationException("prompt_yesno requires a ``` fenced message.");
        }

        var afterOpen = inner[(fenceStart + 3)..];
        var fenceEnd = afterOpen.IndexOf("```", StringComparison.Ordinal);
        if (fenceEnd < 0)
        {
            throw new InvalidOperationException("Unclosed ``` fence in prompt_yesno command.");
        }

        var message = afterOpen[..fenceEnd];
        var labels = afterOpen[(fenceEnd + 3)..].Trim();
        var parts = labels.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new InvalidOperationException("prompt_yesno requires two label options separated by '|'.");
        }

        return (message, parts[0], parts[1]);
    }

    private async Task<bool> ExecuteChooseAsync(
        ParsedTemplate template,
        ChooseNode chooseNode,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        var promptBuilder = new System.Text.StringBuilder();
        var startIndex = context.ProgramCounter;

        while (context.ProgramCounter < template.Nodes.Count)
        {
            var node = template.Nodes[context.ProgramCounter];
            if (node is LabelNode)
            {
                break;
            }

            context.ProgramCounter++;

            switch (node)
            {
                case TextNode textNode:
                    promptBuilder.Append(VariableSubstitution.Substitute(textNode.Text, context));
                    break;

                case DoNode doNode when doNode.Command.Equals("prompt()", StringComparison.OrdinalIgnoreCase):
                    await DisplayInitialHistoryAsync(context, cancellationToken);

                    string? prompt;
                    if (!context.InitialPromptConsumed && !string.IsNullOrWhiteSpace(context.InitialPrompt))
                    {
                        prompt = context.InitialPrompt;
                        context.InitialPromptConsumed = true;
                    }
                    else
                    {
                        prompt = await host.PromptUserAsync(cancellationToken);
                    }

                    if (prompt is null)
                    {
                        return false;
                    }

                    context.Variables["prompt"] = prompt;
                    promptBuilder.Append(prompt);
                    break;

                default:
                    context.ProgramCounter--;
                    goto BuildPrompt;
            }
        }

        BuildPrompt:
        var promptText = promptBuilder.ToString();
        promptText = promptText.Replace(
            "[$history]",
            VariableSubstitution.SerializeHistory(context.History),
            StringComparison.OrdinalIgnoreCase);

        var savedCompletion = context.Variables.TryGetValue("completion", out var completionValue)
            ? completionValue
            : string.Empty;

        string? selected = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (context.ModelFallbacks.Count == 0)
            {
                throw new InvalidOperationException("No model fallbacks configured for choose.");
            }

            var result = await modelRequestService.CompleteAsync(
                context.ModelFallbacks,
                [new ChatMessage { Role = ChatRole.User, Content = promptText }],
                tools: null,
                updateCompletion: false,
                cancellationToken: cancellationToken);

            selected = MatchChooseOption(result.Content, chooseNode.Options);
            if (selected is not null)
            {
                break;
            }

            await host.WriteSystemMessageAsync(
                $"Choose attempt {attempt + 1} did not return a single valid option. Retrying...",
                cancellationToken);
        }

        if (selected is null)
        {
            throw new InvalidOperationException(
                $"Choose failed after 3 attempts. Expected one of: {string.Join(", ", chooseNode.Options)}");
        }

        context.Variables["completion"] = savedCompletion;
        JumpToLabel(template, context, selected);
        return true;
    }

    private static void JumpToLabel(ParsedTemplate template, AgentContext context, string label)
    {
        if (!template.Labels.TryGetValue(label, out var index))
        {
            throw new InvalidOperationException($"Unknown label '{label}'.");
        }

        context.ProgramCounter = index + 1;
    }

    private async Task DisplayInitialHistoryAsync(AgentContext context, CancellationToken cancellationToken)
    {
        if (context.InitialHistoryDisplayed)
        {
            return;
        }

        FlushBuffer(context);

        foreach (var message in context.History)
        {
            await host.WriteHistoryMessageAsync(message.Role, message.Content, cancellationToken);
        }

        context.InitialHistoryDisplayed = true;
    }

    private static void FlushBuffer(AgentContext context)
    {
        if (string.IsNullOrEmpty(context.CurrentBuffer) || context.CurrentRole is null)
        {
            context.CurrentBuffer = string.Empty;
            return;
        }

        if (context.History.Count > 0 &&
            context.History[^1].Role == context.CurrentRole.Value)
        {
            context.History[^1].Content += context.CurrentBuffer;
        }
        else
        {
            context.History.Add(new ChatMessage
            {
                Role = context.CurrentRole.Value,
                Content = context.CurrentBuffer
            });
        }

        context.CurrentBuffer = string.Empty;
    }

    private static string? MatchChooseOption(string output, IReadOnlyList<string> options)
    {
        var normalizedOutput = NormalizeChooseValue(output);
        string? match = null;

        foreach (var option in options)
        {
            var normalizedOption = NormalizeChooseValue(option);
            if (normalizedOutput.Contains(normalizedOption, StringComparison.OrdinalIgnoreCase))
            {
                if (match is not null)
                {
                    return null;
                }

                match = option;
            }
        }

        return match;
    }

    private static string NormalizeChooseValue(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
