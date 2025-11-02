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
using MsOptions = Microsoft.Extensions.Options;

namespace AIReviewer;

/// <summary>
/// Entry point host builder for the AI pull request reviewer console application.
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
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
                    
                    // Load .env file if present (before loading settings)
                    var envFile = Path.Combine(AppContext.BaseDirectory, ".env");
                    var envData = DotEnvParser.Parse(envFile);
                    if (envData.Count > 0)
                    {
                        logger?.LogInformation("Loading {Count} environment variables from .env file", envData.Count);
                        foreach (var (key, value) in envData)
                        {
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                Environment.SetEnvironmentVariable(key, value);
                            }
                        }
                    }
                    
                    // Load settings from settings.ini + environment
                    var options = SettingsLoader.Load(logger);
                    
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
                services.AddSingleton<RetryPolicyFactory>();
                services.AddSingleton<AdoSdkClient>();
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
internal sealed class OptionsMonitorWrapper<T> : MsOptions.IOptionsMonitor<T>
{
    private readonly T _value;
    
    public OptionsMonitorWrapper(T value)
    {
        _value = value;
    }
    
    public T CurrentValue => _value;
    public T Get(string? name) => _value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
