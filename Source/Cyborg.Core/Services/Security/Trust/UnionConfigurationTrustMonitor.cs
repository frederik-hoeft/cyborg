using Cyborg.Core.Services.IO;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Cyborg.Core.Services.Security.Trust.Policies;

namespace Cyborg.Core.Services.Security.Trust;

public sealed class UnionConfigurationTrustMonitor
(
    IServiceProvider serviceProvider,
    IConfigurationTrustPolicyProvider policyProvider,
    IConfigurationTrustOptionsProvider optionsProvider,
    IPathCanonicalizationService pathCanonicalizationService
) : IConfigurationTrustMonitor
{
    private static ConfigurationTrustDecision EnforcementDisabled(string canonicalPath) => new
    (
        Path: canonicalPath,
        IsTrusted: true,
        Decisions:
        [
            new ConfigurationTrustPolicyDecision("cyborg.trust.policy.disabled", ConfigurationTrustDecisionKind.Abstain, Reason: "Trust evaluation is disabled by configuration.")
        ]
    );

    public async ValueTask<ConfigurationTrustDecision> EvaluateAsync(string path, CancellationToken cancellationToken = default)
    {
        string canonicalPath = pathCanonicalizationService.Canonicalize(path);
        if (optionsProvider.Options.EnforcementMode is TrustEnforcementMode.Disabled)
        {
            return EnforcementDisabled(canonicalPath);
        }
        ConfigurationTrustSubject subject = new(canonicalPath);

        List<ConfigurationTrustPolicyDecision> decisions = [];
        bool isTrusted = true;
        foreach (IConfigurationTrustPolicy policy in policyProvider.GetPolicies())
        {
            ConfigurationTrustPolicyDecision decision = await policy.EvaluateAsync(serviceProvider, subject, cancellationToken);
            decisions.Add(decision);
            if (decision.Decision == ConfigurationTrustDecisionKind.Reject)
            {
                isTrusted = false;
            }
        }
        return new ConfigurationTrustDecision(canonicalPath, isTrusted, decisions);
    }
}