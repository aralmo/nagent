using CustomAgents.Core.Domain;
using CustomAgents.Core.Execution;
using CustomAgents.Core.Logging;
using CustomAgents.Core.Parsing;
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

        var templatePath = args[0];
        string? initialPrompt = null;
        var toolFiles = new List<string>();

        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--prompt" && i + 1 < args.Length)
            {
                initialPrompt = args[++i];
                continue;
            }

            if (args[i] == "--tools" && i + 1 < args.Length)
            {
                toolFiles.Add(args[++i]);
                continue;
            }

            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 1;
        }

        if (!File.Exists(templatePath))
        {
            Console.Error.WriteLine($"Template file not found: {templatePath}");
            return 1;
        }

        var workingPath = Directory.GetCurrentDirectory();
        var host = new ConsoleAgentHost();
        await using var logger = JsonlConversationLogger.Create(workingPath);

        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var providers = new ProviderRegistry([
            new OpenRouterProvider(httpClient),
            new OllamaProvider(httpClient)
        ]);

        var modelRequestService = new ModelRequestService(providers, host, logger);
        var shellRunner = new ShellRunner();
        var parser = new TemplateParser();
        var handoverCoordinator = new AgentHandoverCoordinator(parser, logger, host);
        var delegateCoordinator = new AgentDelegateCoordinator(parser, logger, host);
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
        var engine = new AgentEngine(host, turnRunner, modelRequestService, shellRunner, toolRegistry);
        handoverCoordinator.Bind(engine);
        delegateCoordinator.Bind(engine);

        try
        {
            var template = parser.ParseFile(templatePath, workingPath);
            var context = new AgentContext
            {
                WorkingPath = workingPath,
                TemplatePath = Path.GetFullPath(templatePath),
                InitialPrompt = initialPrompt
            };

            await engine.RunAsync(template, context);
            Console.WriteLine();
            Console.WriteLine($"Log written to: {logger.LogFilePath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: customagent <template.md> [--prompt <initial prompt>] [--tools <tools.json>]...");
    }
}
