using System.Text.Json;
using System.Text.Json.Nodes;
using CustomAgents.Core.Domain;
using CustomAgents.Core.Hosting;

namespace CustomAgents.Cli;

public sealed class ConsoleAgentHost : IAgentHost
{
    private bool _thinkingActive;
    private bool _responseActive;

    public Task<string?> PromptUserAsync(CancellationToken cancellationToken = default)
    {
        EndResponse();
        Console.WriteLine();
        WriteColored("[prompt] ", ConsoleColor.White);
        Console.Write("> ");
        var input = Console.ReadLine();
        if (input is null || IsQuitCommand(input))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(input);
    }

    public Task<string?> PromptChoiceAsync(
        string message,
        string optionYes,
        string optionNo,
        CancellationToken cancellationToken = default)
    {
        EndResponse();
        Console.WriteLine();
        WriteColored(message, ConsoleColor.White);
        Console.WriteLine();

        while (true)
        {
            WriteColored($"[{optionYes}] / [{optionNo}] ", ConsoleColor.White);
            Console.Write("> ");
            var input = Console.ReadLine();
            if (input is null || IsQuitCommand(input))
            {
                return Task.FromResult<string?>(null);
            }

            var normalizedInput = NormalizeChoiceInput(input);
            var normalizedYes = NormalizeChoiceInput(optionYes);
            var normalizedNo = NormalizeChoiceInput(optionNo);

            if (normalizedInput.Equals(normalizedYes, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<string?>(optionYes);
            }

            if (normalizedInput.Equals(normalizedNo, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<string?>(optionNo);
            }

            WriteColored($"Invalid choice. Enter '{optionYes}' or '{optionNo}'.", ConsoleColor.DarkGray);
            Console.WriteLine();
        }
    }

    private static string NormalizeChoiceInput(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal);

    private static bool IsQuitCommand(string input)
    {
        var normalized = input.Trim();
        return normalized.Equals("quit", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("/quit", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("exit", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("/exit", StringComparison.OrdinalIgnoreCase);
    }

    public Task WriteStreamDeltaAsync(string delta, CancellationToken cancellationToken = default)
    {
        EndThinking();
        BeginResponse();
        Console.Write(delta);
        return Task.CompletedTask;
    }

    public Task WriteThinkingDeltaAsync(string delta, CancellationToken cancellationToken = default)
    {
        BeginThinking();
        WriteColored(delta, ConsoleColor.DarkGray);
        return Task.CompletedTask;
    }

    public Task WriteToolCallAsync(string name, string argumentsJson, CancellationToken cancellationToken = default)
    {
        EndResponse();
        Console.WriteLine();
        WriteColored($"[tool-call] {name}", ConsoleColor.Cyan);
        Console.WriteLine();
        WriteColored(FormatJson(argumentsJson), ConsoleColor.DarkCyan);
        return Task.CompletedTask;
    }

    public Task WriteToolResponseAsync(string name, string content, CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        WriteColored($"[tool-response] {name}", ConsoleColor.Yellow);
        Console.WriteLine();
        WriteColored(Truncate(content, 4000), ConsoleColor.DarkYellow);
        Console.WriteLine();
        return Task.CompletedTask;
    }

    public Task WriteSystemMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        EndResponse();
        Console.WriteLine();
        WriteColored($"[system] {message}", ConsoleColor.DarkGray);
        return Task.CompletedTask;
    }

    public Task WriteHistoryMessageAsync(ChatRole role, string content, CancellationToken cancellationToken = default)
    {
        EndResponse();
        Console.WriteLine();
        var (label, color) = role switch
        {
            ChatRole.System => ("[system]", ConsoleColor.DarkGray),
            ChatRole.Assistant => ("[assistant]", ConsoleColor.Green),
            ChatRole.User => ("[user]", ConsoleColor.White),
            ChatRole.Tool => ("[tool]", ConsoleColor.Yellow),
            _ => ("[message]", ConsoleColor.Gray)
        };

        WriteColored(label, color);
        Console.WriteLine();
        WriteColored(content, color);
        Console.WriteLine();
        return Task.CompletedTask;
    }

    private void BeginThinking()
    {
        if (_thinkingActive)
        {
            return;
        }

        EndResponse();
        Console.WriteLine();
        WriteColored("[thinking] ", ConsoleColor.DarkGray);
        _thinkingActive = true;
    }

    private void EndThinking()
    {
        if (!_thinkingActive)
        {
            return;
        }

        Console.WriteLine();
        _thinkingActive = false;
    }

    private void BeginResponse()
    {
        if (_responseActive)
        {
            return;
        }

        Console.WriteLine();
        WriteColored("[assistant] ", ConsoleColor.Green);
        _responseActive = true;
    }

    private void EndResponse()
    {
        if (!_responseActive)
        {
            return;
        }

        Console.WriteLine();
        _responseActive = false;
    }

    private static void WriteColored(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = previous;
    }

    private static string FormatJson(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string Truncate(string content, int maxLength)
    {
        if (content.Length <= maxLength)
        {
            return content;
        }

        return content[..maxLength] + Environment.NewLine + "...[truncated]";
    }
}
