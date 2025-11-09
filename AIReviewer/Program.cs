using AIReviewer.AI;
using AIReviewer.AzureDevOps;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Policy;
using AIReviewer.Review;
using AIReviewer.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using MsOptions = Microsoft.Extensions.Options;

namespace AIReviewer;

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
            .ConfigureLogging((ctx, logging) =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(conf =>
                {
                    conf.IncludeScopes = true;
                    conf.TimestampFormat = "HH:mm:ss ";
                });
            })
            .ConfigureServices((ctx, services) =>
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
                services.AddSingleton<IAdoSdkClient, AdoSdkClient>();
                services.AddSingleton<CommentService>();
                services.AddSingleton<ApprovalService>();
                services.AddSingleton<DiffService>();
                services.AddSingleton<StateStore>();
                services.AddSingleton<ReviewPlanner>();
                services.AddSingleton<ReviewContextRetriever>();
                services.AddSingleton<PromptBuilder>();
                services.AddSingleton<IAiClient, AzureFoundryAiClient>();

                services.AddHostedService<ReviewerHostedService>();
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
    public T Get(string? name) => _value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
