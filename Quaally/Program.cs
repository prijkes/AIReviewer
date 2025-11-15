using Quaally.AI;
using Quaally.AzureDevOps;
using Quaally.AzureDevOps.Functions;
using Quaally.Options;
using Quaally.Orchestration;
using Quaally.Policy;
using Quaally.Queue;
using Quaally.Review;
using Quaally.Utils;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.CommandLine;
using MsOptions = Microsoft.Extensions.Options;

namespace Quaally;

/// <summary>
/// Entry point host builder for the AI pull request reviewer console application.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Define command-line options using System.CommandLine
        Option<string> settingsOption = new("--settings")
        {
            Description = "Path to settings.ini file",
            DefaultValueFactory = _ => "settings.ini"
        };
        settingsOption.Aliases.Add("-s");

        Option<string?> envOption = new("--env")
        {
            Description = "Path to .env file (optional)"
        };
        envOption.Aliases.Add("-e");

        // Create root command
        RootCommand rootCommand = new("AIReviewer - AI-powered pull request reviewer");
        rootCommand.Options.Add(settingsOption);
        rootCommand.Options.Add(envOption);

        // Set the action handler
        rootCommand.SetAction(async parseResult =>
        {
            var settingsPath = parseResult.GetValue(settingsOption)!;
            var envPath = parseResult.GetValue(envOption);
            await RunApplication(settingsPath, envPath);
            return 0;
        });

        // Parse and invoke
        return await rootCommand.Parse(args).InvokeAsync();
    }

    /// <summary>
    /// Runs the main application with the specified configuration paths.
    /// </summary>
    private static async Task RunApplication(string settingsPath, string? envPath)
    {
        var host = Host.CreateDefaultBuilder([])
            .UseConsoleLifetime()
            .ConfigureLogging((_, logging) =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(conf =>
                {
                    conf.IncludeScopes = true;
                    conf.TimestampFormat = "HH:mm:ss ";
                });
            })
            .ConfigureServices((_, services) =>
            {
                // Load configuration from settings.ini + environment variables
                // This is done early so we can use logging for diagnostic messages
                services.AddSingleton(sp =>
                {
                    var logger = sp.GetService<ILogger<ReviewerOptions>>();

                    // Load .env file if specified (before loading settings)
                    if (envPath != null)
                    {
                        var envData = DotEnvParser.Parse(envPath);
                        if (envData.Count > 0)
                        {
                            logger?.LogInformation("Loading {Count} environment variables from {EnvFile}", envData.Count, envPath);
                            foreach (var (key, value) in envData)
                            {
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    Environment.SetEnvironmentVariable(key, value);
                                }
                            }
                        }
                        else
                        {
                            logger?.LogWarning("No environment variables found in {EnvFile}", envPath);
                        }
                    }

                    // Load settings from settings.ini + environment
                    var options = SettingsLoader.Load(logger, settingsPath);

                    // Normalize paths
                    options.Normalize();

                    return MsOptions.Options.Create(options);
                });

                // Register as singleton for direct access
                services.AddSingleton(sp => sp.GetRequiredService<MsOptions.IOptions<ReviewerOptions>>().Value);

                // Register as IOptionsMonitor for components that need it
                services.AddSingleton<MsOptions.IOptionsMonitor<ReviewerOptions>>(sp =>
                {
                    var options = sp.GetRequiredService<MsOptions.IOptions<ReviewerOptions>>().Value;
                    return new OptionsMonitorWrapper<ReviewerOptions>(options);
                });

                services.AddSingleton<PolicyLoader>();
                services.AddSingleton<PromptLoader>();
                services.AddSingleton<RetryPolicyFactory>();
                
                // Legacy Azure DevOps services (kept for backward compatibility)
                services.AddSingleton<IAdoSdkClient, AdoSdkClient>();
                services.AddSingleton<CommentService>();
                services.AddSingleton<ApprovalService>();
                
                // Azure DevOps Provider services
                services.AddSingleton<Quaally.Providers.AzureDevOps.AzureDevOpsSourceControlClient>();
                services.AddSingleton<Quaally.Providers.AzureDevOps.AzureDevOpsCommentService>();
                services.AddSingleton<Quaally.Providers.AzureDevOps.AzureDevOpsApprovalService>();
                
                // Core interface implementations (provider-agnostic)
                services.AddSingleton<Core.Interfaces.ISourceControlClient>(sp =>
                {
                    var options = sp.GetRequiredService<ReviewerOptions>();
                    return options.SourceProvider switch
                    {
                        Core.Enums.SourceProvider.AzureDevOps => sp.GetRequiredService<Providers.AzureDevOps.AzureDevOpsSourceControlClient>(),
                        _ => throw new NotSupportedException($"Source provider {options.SourceProvider} is not supported")
                    };
                });
                
                services.AddSingleton<Core.Interfaces.ICommentService>(sp =>
                {
                    var options = sp.GetRequiredService<ReviewerOptions>();
                    return options.SourceProvider switch
                    {
                        Core.Enums.SourceProvider.AzureDevOps => sp.GetRequiredService<Providers.AzureDevOps.AzureDevOpsCommentService>(),
                        _ => throw new NotSupportedException($"Source provider {options.SourceProvider} is not supported")
                    };
                });
                
                services.AddSingleton<Core.Interfaces.IApprovalService>(sp =>
                {
                    var options = sp.GetRequiredService<ReviewerOptions>();
                    return options.SourceProvider switch
                    {
                        Core.Enums.SourceProvider.AzureDevOps => sp.GetRequiredService<Providers.AzureDevOps.AzureDevOpsApprovalService>(),
                        _ => throw new NotSupportedException($"Source provider {options.SourceProvider} is not supported")
                    };
                });
                
                services.AddSingleton<StateStore>();
                services.AddSingleton<ReviewPlanner>();
                services.AddSingleton<ReviewContextRetriever>();
                services.AddSingleton<PromptBuilder>();
                services.AddSingleton<IAiClient, AzureFoundryAiClient>();

                // Register ChatClient for OpenAI function calling
                services.AddSingleton(sp =>
                {
                    var options = sp.GetRequiredService<ReviewerOptions>();
                    var endpoint = new Uri(options.AiFoundryEndpoint);
                    var credential = new DefaultAzureCredential();
                    var azureClient = new AzureOpenAIClient(endpoint, credential);
                    return azureClient.GetChatClient(options.AiFoundryDeployment);
                });

                // Register new queue-based services
                services.AddSingleton<BotIdentityService>();
                services.AddSingleton<MentionDetector>();
                services.AddSingleton<AzureDevOpsFunctionExecutor>();
                services.AddSingleton<AiOrchestrator>();

                // Replace ReviewerHostedService with QueueProcessorHostedService
                services.AddHostedService<QueueProcessorHostedService>();
            })
            .Build();

        await host.RunAsync();
    }
}

/// <summary>
/// Simple wrapper to provide IOptionsMonitor from a static value.
/// </summary>
internal sealed class OptionsMonitorWrapper<T>(T value) : MsOptions.IOptionsMonitor<T>
{
    private readonly T _value = value;

    public T CurrentValue => _value;
    public T Get(string? _) => _value;
    public IDisposable? OnChange(Action<T, string?> _) => null;
}
