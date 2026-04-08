using Cyborg.Core.Services.Security.Trust.Configuration;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Services.Security.Trust;

public interface IConfigurationTrustMonitor
{
    ValueTask<ConfigurationTrustDecision> EvaluateAsync(string path, CancellationToken cancellationToken = default);
}

public interface IConfigurationTrustService
{
    void Enforce(ConfigurationTrustDecision decision);
}

public sealed class DefaultConfigurationTrustService(IConfigurationTrustOptionsProvider optionsProvider, ILoggerFactory loggerFactory) : IConfigurationTrustService
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.trust");

    public void Enforce(ConfigurationTrustDecision decision)
    {
        if (optionsProvider.Options.EnforcementMode is TrustEnforcementMode.Disabled)
        {
            return;
        }
        // TODO: Implement trust enforcement logic based on the decision and the configured enforcement mode.
    }
}