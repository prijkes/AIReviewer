using System.ComponentModel.DataAnnotations;
using AIReviewer.AI;
using AIReviewer.AzureDevOps;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Policy;
using AIReviewer.Review;
using AIReviewer.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            .ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddEnvironmentVariables();
                var envFile = Path.Combine(AppContext.BaseDirectory, ".env");
                var envData = DotEnvParser.Parse(envFile);
                if (envData.Count > 0)
                {
                    cfg.AddInMemoryCollection(envData);
                }
                cfg.AddEnvironmentVariables();
            })
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
                services.AddOptions<ReviewerOptions>()
                    .Bind(ctx.Configuration)
                    .Validate(options =>
                    {
                        var validationResults = new List<ValidationResult>();
                        var validationContext = new ValidationContext(options);
                        return Validator.TryValidateObject(options, validationContext, validationResults, validateAllProperties: true);
                    }, "Configuration failed validation.")
                    .Validate(options => !string.IsNullOrWhiteSpace(options.AdoAccessToken), "ADO_ACCESS_TOKEN is required.")
                    .ValidateOnStart();

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
