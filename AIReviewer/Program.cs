using System.Collections.Generic;
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
                if (File.Exists(envFile))
                {
                    var envData = ParseDotEnv(envFile);
                    cfg.AddInMemoryCollection(envData);
                }
                cfg.AddEnvironmentVariables();
            })
            .ConfigureLogging((ctx, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole(opts =>
                {
                    opts.IncludeScopes = true;
                    opts.TimestampFormat = "HH:mm:ss ";
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
                services.AddSingleton<IAiClient, AzureFoundryAiClient>();

                services.AddHostedService<ReviewerHostedService>();
            })
            .Build();

        await host.RunAsync();
    }

    /// <summary>
    /// Parses a POSIX-style .env file and hydrates environment variables into configuration.
    /// </summary>
    private static IDictionary<string, string?> ParseDotEnv(string envFile)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(envFile))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var line = rawLine.Trim();
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                line = line["export ".Length..].TrimStart();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
                 (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))))
            {
                value = value[1..^1];
            }

            data[key] = value;
            Environment.SetEnvironmentVariable(key, value);
        }

        return data;
    }
}
