using CustomAgents.Core.Domain;
using CustomAgents.Core.Execution;
using CustomAgents.Core.Logging;
using CustomAgents.Core.Parsing;
using CustomAgents.Core.Persistence;
using CustomAgents.Core.Providers;
using CustomAgents.Core.Shell;
using CustomAgents.Core.Tools;

namespace CustomAgents.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        if (!TryParseArgs(args, out var launch, out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 1;
        }

        if (launch.IsResume)
        {
            return await RunResumeAsync(launch.ResumeSessionId!);
        }

        if (!File.Exists(launch.TemplatePath!))
        {
            Console.Error.WriteLine($"Template file not found: {launch.TemplatePath}");
            return 1;
        }

        return await RunNewSessionAsync(launch);
    }

    private static async Task<int> RunNewSessionAsync(LaunchArgs launch)
    {
        var workingPath = Directory.GetCurrentDirectory();
        var sessionId = Guid.NewGuid().ToString();
        var createdAt = DateTimeOffset.UtcNow;
        var sessionStore = new SessionStore();

        var host = new ConsoleAgentHost();
        await using var logger = JsonlConversationLogger.Create(workingPath);

        var sessionMetadata = new SessionMetadata
        {
            SessionId = sessionId,
            ToolFiles = [.. launch.ToolFiles],
            LogFilePath = logger.LogFilePath,
            CreatedAt = createdAt
        };
        var checkpointService = new SessionCheckpointService(sessionStore, sessionMetadata);

        var engine = BuildEngine(host, logger, checkpointService, launch.ToolFiles);

        await logger.LogAsync(new
        {
            type = "conversation_start",
            timestamp = DateTimeOffset.UtcNow,
            sessionId,
            agent = launch.TemplatePath,
            prompt = launch.InitialPrompt
        });

        try
        {
            var parser = new TemplateParser();
            var template = parser.ParseFile(launch.TemplatePath!, workingPath);
            var context = new AgentContext
            {
                WorkingPath = workingPath,
                TemplatePath = Path.GetFullPath(launch.TemplatePath!),
                InitialPrompt = launch.InitialPrompt
            };
            context.InitializeVariables();

            await checkpointService.SaveAsync(context);

            await engine.RunAsync(template, context);
            await checkpointService.SaveAsync(context);

            PrintExitHint(sessionId, logger.LogFilePath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            PrintExitHint(sessionId, logger.LogFilePath);
            return 1;
        }
    }

    private static async Task<int> RunResumeAsync(string sessionId)
    {
        var sessionStore = new SessionStore();
        var session = sessionStore.TryLoadSession(sessionId);
        if (session is null)
        {
            Console.Error.WriteLine($"Session not found: {sessionId}");
            return 1;
        }

        if (!File.Exists(session.TemplatePath))
        {
            Console.Error.WriteLine($"Template file not found: {session.TemplatePath}");
            return 1;
        }

        Directory.SetCurrentDirectory(session.WorkingPath);

        var host = new ConsoleAgentHost();
        await using var logger = !string.IsNullOrEmpty(session.LogFilePath) && File.Exists(session.LogFilePath)
            ? JsonlConversationLogger.OpenExisting(session.LogFilePath)
            : JsonlConversationLogger.Create(session.WorkingPath);

        var sessionMetadata = new SessionMetadata
        {
            SessionId = session.SessionId,
            ToolFiles = [.. session.ToolFiles],
            LogFilePath = logger.LogFilePath,
            CreatedAt = session.CreatedAt
        };
        var checkpointService = new SessionCheckpointService(sessionStore, sessionMetadata, session);

        var engine = BuildEngine(host, logger, checkpointService, session.ToolFiles);

        try
        {
            var parser = new TemplateParser();
            var template = parser.ParseFile(session.TemplatePath, session.WorkingPath);
            var context = new AgentContext
            {
                WorkingPath = session.WorkingPath
            };
            AgentContextMapper.ApplyToContext(session, context);
            context.RefreshDateTime();

            await engine.RunAsync(template, context, initializeVariables: false);
            await checkpointService.SaveAsync(context);

            PrintExitHint(sessionId, logger.LogFilePath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            PrintExitHint(sessionId, logger.LogFilePath);
            return 1;
        }
    }

    private static AgentEngine BuildEngine(
        ConsoleAgentHost host,
        JsonlConversationLogger logger,
        SessionCheckpointService checkpointService,
        IReadOnlyList<string> toolFiles)
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var providers = new ProviderRegistry([
            new OpenRouterProvider(httpClient),
            new OllamaProvider(httpClient)
        ]);

        var modelRequestService = new ModelRequestService(providers, host, logger);
        var shellRunner = new ShellRunner();
        var parser = new TemplateParser();
        var handoverCoordinator = new AgentHandoverCoordinator(parser, logger, host, checkpointService);
        var delegateCoordinator = new AgentDelegateCoordinator(parser, logger, host, checkpointService);
        var toolRegistry = new ToolRegistry([
            new FileReadTool(),
            new FileWriteTool(),
            new FileSearchTool(),
            new ShellTool(shellRunner),
            new AgentHandoverTool(),
            new AgentDelegateTool()
        ], shellRunner);

        foreach (var toolFile in toolFiles)
        {
            toolRegistry.LoadFromFile(toolFile);
        }

        var turnRunner = new TurnRunner(
            modelRequestService,
            toolRegistry,
            host,
            logger,
            handoverCoordinator,
            delegateCoordinator);
        var engine = new AgentEngine(host, turnRunner, modelRequestService, shellRunner, toolRegistry, checkpointService);
        handoverCoordinator.Bind(engine);
        delegateCoordinator.Bind(engine);

        return engine;
    }

    private static void PrintExitHint(string sessionId, string logFilePath)
    {
        Console.WriteLine();
        Console.WriteLine($"Session: {sessionId}");
        Console.WriteLine($"Resume with: customagent --resume {sessionId}");
        Console.WriteLine($"Log written to: {logFilePath}");
    }

    internal static bool TryParseArgs(string[] args, out LaunchArgs launch, out string error)
    {
        launch = new LaunchArgs();
        error = string.Empty;

        if (args[0] == "--resume")
        {
            if (args.Length < 2)
            {
                error = "Missing session id for --resume.";
                return false;
            }

            launch.IsResume = true;
            launch.ResumeSessionId = args[1];
            return true;
        }

        launch.TemplatePath = args[0];
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--prompt" && i + 1 < args.Length)
            {
                launch.InitialPrompt = args[++i];
                continue;
            }

            if (args[i] == "--tools" && i + 1 < args.Length)
            {
                launch.ToolFiles.Add(args[++i]);
                continue;
            }

            error = $"Unknown argument: {args[i]}";
            return false;
        }

        return true;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  customagent <template.md> [--prompt <initial prompt>] [--tools <tools.json>]...");
        Console.WriteLine("  customagent --resume <sessionId>");
    }

    internal sealed class LaunchArgs
    {
        public bool IsResume { get; set; }
        public string? ResumeSessionId { get; set; }
        public string? TemplatePath { get; set; }
        public string? InitialPrompt { get; set; }
        public List<string> ToolFiles { get; } = [];
    }
}
