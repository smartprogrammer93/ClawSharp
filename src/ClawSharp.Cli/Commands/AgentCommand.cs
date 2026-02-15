using System.CommandLine;
using ClawSharp.Agent;
using ClawSharp.Core.Providers;
using ClawSharp.Infrastructure;
using ClawSharp.Infrastructure.Config;
using Microsoft.Extensions.DependencyInjection;

namespace ClawSharp.Cli.Commands;

public class AgentCommand : Command
{
    private static readonly Option<string?> MessageOption = new("-m", "--message") { Description = "Send a single message" };
    private static readonly Option<string?> ProviderOption = new("-p", "--provider") { Description = "LLM provider to use" };
    private static readonly Option<string?> ModelOption = new("--model") { Description = "Model to use" };

    public AgentCommand() : base("agent", "Chat with the AI agent")
    {
        Add(MessageOption);
        Add(ProviderOption);
        Add(ModelOption);
        SetAction(ctx =>
        {
            var message = ctx.GetValue(MessageOption);
            var provider = ctx.GetValue(ProviderOption);
            var model = ctx.GetValue(ModelOption);
            return ExecuteAsync(message, provider, model);
        });
    }

    private static async Task ExecuteAsync(string? message, string? provider, string? model)
    {

        // Load configuration
        var config = ConfigLoader.LoadConfig();

        // Build DI container
        var services = new ServiceCollection();
        services.AddClawSharp(config);
        var serviceProvider = services.BuildServiceProvider();

        // Get services
        var sessionManager = serviceProvider.GetRequiredService<Core.Sessions.ISessionManager>();
        var contextBuilder = serviceProvider.GetRequiredService<ContextBuilder>();
        var providerResolver = serviceProvider.GetRequiredService<Func<string, ILlmProvider>>();
        var toolRegistry = serviceProvider.GetRequiredService<Core.Tools.IToolRegistry>();
        var messageBus = serviceProvider.GetRequiredService<Core.Channels.IMessageBus>();

        // Determine provider and model
        var selectedProvider = provider ?? config.DefaultProvider ?? "openai";
        var llmProvider = providerResolver(selectedProvider);
        var selectedModel = model ?? config.DefaultModel ?? "gpt-4o";

        if (message != null)
        {
            // Single-shot mode
            await RunSingleMessageAsync(
                message,
                selectedModel,
                llmProvider,
                sessionManager,
                contextBuilder,
                toolRegistry,
                messageBus
            );
        }
        else
        {
            // REPL mode
            await RunReplAsync(
                selectedModel,
                llmProvider,
                sessionManager,
                contextBuilder,
                toolRegistry,
                messageBus
            );
        }
    }

    private static async Task RunSingleMessageAsync(
        string message,
        string model,
        ILlmProvider provider,
        Core.Sessions.ISessionManager sessionManager,
        ContextBuilder contextBuilder,
        Core.Tools.IToolRegistry toolRegistry,
        Core.Channels.IMessageBus messageBus)
    {
        var session = await sessionManager.GetOrCreateAsync("cli:default", "cli", "default");

        // Add user message to history
        session.History.Add(new LlmMessage("user", message));

        // Build context
        var messages = await contextBuilder.BuildContextAsync(session.History);

        // Create agent loop
        var agentLoop = new AgentLoop(provider, toolRegistry, messageBus, null!);
        var request = new AgentLoop.AgentRequest(model, messages);
        var result = await agentLoop.RunAsync(request);

        // Add assistant response to history
        session.History.Add(new LlmMessage("assistant", result.Content));

        // Save session
        await sessionManager.SaveAsync(session);

        // Print result
        Console.WriteLine(result.Content);

        if (result.ToolExecutions.Count > 0)
        {
            Console.WriteLine($"\n[Used {result.ToolExecutions.Count} tool(s)]");
        }
    }

    private static async Task RunReplAsync(
        string model,
        ILlmProvider provider,
        Core.Sessions.ISessionManager sessionManager,
        ContextBuilder contextBuilder,
        Core.Tools.IToolRegistry toolRegistry,
        Core.Channels.IMessageBus messageBus)
    {
        Console.WriteLine("ClawSharp Agent REPL");
        Console.WriteLine("Type your message and press Enter. Type /exit to quit.");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine($"Provider: {provider.Name}");
        Console.WriteLine();

        var session = await sessionManager.GetOrCreateAsync("cli:default", "cli", "default");

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Handle slash commands
            if (input == "/exit" || input == "/quit")
                break;

            if (input == "/clear")
            {
                session.History.Clear();
                await sessionManager.SaveAsync(session);
                Console.WriteLine("Session history cleared.");
                continue;
            }

            if (input == "/help")
            {
                Console.WriteLine("Available commands:");
                Console.WriteLine("  /exit   - Exit the REPL");
                Console.WriteLine("  /clear  - Clear conversation history");
                Console.WriteLine("  /help   - Show this help");
                continue;
            }

            // Add user message
            session.History.Add(new LlmMessage("user", input));

            // Build context
            var messages = await contextBuilder.BuildContextAsync(session.History);

            // Create agent loop
            var agentLoop = new AgentLoop(provider, toolRegistry, messageBus, null!);
            var request = new AgentLoop.AgentRequest(model, messages);
            var result = await agentLoop.RunAsync(request);

            // Add assistant response
            session.History.Add(new LlmMessage("assistant", result.Content));

            // Save session
            await sessionManager.SaveAsync(session);

            // Print result
            Console.WriteLine(result.Content);

            if (result.ToolExecutions.Count > 0)
            {
                Console.WriteLine($"\n[Used {result.ToolExecutions.Count} tool(s)]");
            }

            Console.WriteLine();
        }

        Console.WriteLine("Goodbye!");
    }
}
