using Microsoft.Extensions.Logging;
using Quaaly.Infrastructure.AzureDevOps;

namespace Quaaly.Worker.Orchestration;

/// <summary>
/// Service for managing bot identity information.
/// Retrieves and caches the bot user's identity from Azure DevOps based on the PAT.
/// </summary>
public sealed class BotIdentityService(ILogger<BotIdentityService> logger, IAdoSdkClient adoClient)
{
    private string? _botUserId;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Gets the bot user's unique identifier as a string.
    /// Resolves the identity on first call and caches for subsequent calls.
    /// </summary>
    public async Task<string> GetBotUserIdAsync(CancellationToken cancellationToken = default)
    {
        if (_botUserId != null)
        {
            return _botUserId;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_botUserId != null)
            {
                return _botUserId;
            }

            logger.LogInformation("Resolving bot user identity from PAT...");
            
            var identity = adoClient.GetAuthorizedIdentity();
            _botUserId = identity.Id.ToString();

            logger.LogInformation("Bot identity resolved: Id={BotId}, DisplayName={DisplayName}, UniqueName={UniqueName}",
                _botUserId, identity.DisplayName, identity.UniqueName);

            return _botUserId;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
