using Quaally.Infrastructure.AI;
using Quaally.Infrastructure.AzureDevOps;
using Quaally.Infrastructure.AzureDevOps.Functions;
using Quaally.Infrastructure.Options;
using Quaally.Worker.Orchestration;
using Quaally.Infrastructure.Policy;
using Quaally.Worker.Queue;
using Quaally.Infrastructure.Review;
using Quaally.Infrastructure.Utils;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using MsOptions = Microsoft.Extensions.Options;

namespace Quaally.Worker;

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
            Description = "Path to main settings.ini file",
            DefaultValueFactory = _ => "settings.ini"
        };
        settingsOption.Aliases.Add("-s");

        Option<string?> providerSettingsOption = new("--provider-settings")
        {
            Description = "Path to provider-specific settings file (e.g., azure.ini, github.ini)"
        };
        providerSettingsOption.Aliases.Add("-ps");

        Option<string?> providerEnvOption = new("--provider-env")
        {
            Description = "Path to provider-specific .env file (e.g., azure.env, github.env)"
        };
        providerEnvOption.Aliases.Add("-pe");

        // Create root command
        RootCommand rootCommand = new("Quaally - AI-powered pull request reviewer");
        rootCommand.Options.Add(settingsOption);
        rootCommand.Options.Add(providerSettingsOption);
        rootCommand.Options.Add(providerEnvOption);

        // Set the action handler
        rootCommand.SetAction(async parseResult =>
        {
            var settingsPath = parseResult.GetValue(settingsOption)!;
            var providerSettingsPath = parseResult.GetValue(providerSettingsOption);
            var providerEnvPath = parseResult.GetValue(providerEnvOption);
            await RunApplication(settingsPath, providerSettingsPath, providerEnvPath);
            return 0;
        });

        // Parse and invoke
        return await rootCommand.Parse(args).InvokeAsync();
    }

    /// <summary>
    /// Runs the main application with the specified configuration paths.
    /// </summary>
    private static async Task RunApplication(string settingsPath, string? providerSettingsPath, string? providerEnvPath)
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
                // Load configuration from settings.ini + provider-specific files
                // This is done early so we can use logging for diagnostic messages
                services.AddSingleton(sp =>
                {
                    var logger = sp.GetService<ILogger<ReviewerOptions>>();

                    // Load provider-specific .env file first (if specified)
                    if (providerEnvPath != null)
                    {
                        var providerEnvData = DotEnvParser.Parse(providerEnvPath);
                        if (providerEnvData.Count > 0)
                        {
                            logger?.LogInformation("Loading {Count} provider-specific environment variables from {ProviderEnvFile}", providerEnvData.Count, providerEnvPath);
                            foreach (var (key, value) in providerEnvData)
                            {
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    Environment.SetEnvironmentVariable(key, value);
                                }
                            }
                        }
                        else
                        {
                            logger?.LogWarning("No environment variables found in {ProviderEnvFile}", providerEnvPath);
                        }
                    }

                    // Load main settings from settings.ini + environment variables
                    var options = SettingsLoader.Load(logger, settingsPath);

                    // Load and merge provider-specific settings if specified
                    if (providerSettingsPath != null)
                    {
                        logger?.LogInformation("Loading provider-specific settings from {ProviderSettingsFile}", providerSettingsPath);
                        var providerOptions = SettingsLoader.Load(logger, providerSettingsPath);
                        // Provider settings override main settings
                        MergeProviderSettings(options, providerOptions, logger);
                    }

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
                services.AddSingleton<Infrastructure.Providers.AzureDevOps.AzureDevOpsSourceControlClient>();
                services.AddSingleton<Infrastructure.Providers.AzureDevOps.AzureDevOpsCommentService>();
                services.AddSingleton<Infrastructure.Providers.AzureDevOps.AzureDevOpsApprovalService>();
                
                // Core interface implementations (provider-agnostic)
                services.AddSingleton<Core.Interfaces.ISourceControlClient>(sp =>
                {
                    var options = sp.GetRequiredService<ReviewerOptions>();
                    return options.SourceProvider switch
                    {
                        Core.Enums.SourceProvider.AzureDevOps => sp.GetRequiredService<Infrastructure.Providers.AzureDevOps.AzureDevOpsSourceControlClient>(),
                        _ => throw new NotSupportedException($"Source provider {options.SourceProvider} is not supported")
                    };
                });
                
                services.AddSingleton<Core.Interfaces.ICommentService>(sp =>
                {
                    var options = sp.GetRequiredService<ReviewerOptions>();
                    return options.SourceProvider switch
                    {
                        Core.Enums.SourceProvider.AzureDevOps => sp.GetRequiredService<Infrastructure.Providers.AzureDevOps.AzureDevOpsCommentService>(),
                        _ => throw new NotSupportedException($"Source provider {options.SourceProvider} is not supported")
                    };
                });
                
                services.AddSingleton<Core.Interfaces.IApprovalService>(sp =>
                {
                    var options = sp.GetRequiredService<ReviewerOptions>();
                    return options.SourceProvider switch
                    {
                        Core.Enums.SourceProvider.AzureDevOps => sp.GetRequiredService<Infrastructure.Providers.AzureDevOps.AzureDevOpsApprovalService>(),
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

    /// <summary>
    /// Merges provider-specific settings into main settings.
    /// Provider settings take precedence over main settings.
    /// </summary>
    private static void MergeProviderSettings(ReviewerOptions mainOptions, ReviewerOptions providerOptions, ILogger? logger)
    {
        // Merge Queue settings (provider-specific)
        if (!string.IsNullOrWhiteSpace(providerOptions.Queue.QueueName))
            mainOptions.Queue.QueueName = providerOptions.Queue.QueueName;
        
        if (!string.IsNullOrWhiteSpace(providerOptions.Queue.BotDisplayName))
            mainOptions.Queue.BotDisplayName = providerOptions.Queue.BotDisplayName;
        
        if (providerOptions.Queue.MaxConcurrentCalls > 0)
            mainOptions.Queue.MaxConcurrentCalls = providerOptions.Queue.MaxConcurrentCalls;
        
        if (providerOptions.Queue.MaxWaitTimeSeconds > 0)
            mainOptions.Queue.MaxWaitTimeSeconds = providerOptions.Queue.MaxWaitTimeSeconds;

        // Source provider setting
        if (providerOptions.SourceProvider != Core.Enums.SourceProvider.AzureDevOps)
            mainOptions.SourceProvider = providerOptions.SourceProvider;

        logger?.LogDebug("Merged provider-specific settings into main configuration");
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
