using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Cyborg.Core.Services.Security.Trust.Logging;
using Microsoft.Extensions.Logging;
using System.Security;

namespace Cyborg.Core.Services.Security.Trust;

public sealed class DefaultConfigurationTrustService
(
    IConfigurationTrustOptionsProvider optionsProvider,
    ILoggerFactory loggerFactory,
    IJsonLoaderContext jsonContext
) : IConfigurationTrustService
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.trust");

    public void Enforce(ConfigurationTrustDecision decision)
    {
        if (optionsProvider.Options.EnforcementMode is TrustEnforcementMode.Disabled)
        {
            return;
        }
        _logger.LogConfigurationTrustDecision(jsonContext, decision);
        if (!decision.IsTrusted && optionsProvider.Options.EnforcementMode is TrustEnforcementMode.Enforce)
        {
            ThrowNotTrustedException(decision);
        }
    }

    [DoesNotReturn]
    private static void ThrowNotTrustedException(ConfigurationTrustDecision decision) => 
        throw new SecurityException($"Audit failed: configuration at path '{decision.Path}' is not trusted. Decisions: '{string.Join(", ", decision.Decisions
            .Where(static d => d.Decision is ConfigurationTrustDecisionKind.Reject)
            .Select(static d => d.ToString()))}'");
}