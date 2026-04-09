using Cyborg.Core.Services.Security.Trust.Configuration;
using Cyborg.Core.Services.Security.Trust.Policies;

namespace Cyborg.Core.Services.Security.Trust;

public sealed class UnionConfigurationTrustMonitor
(
    IServiceProvider serviceProvider,
    IConfigurationTrustPolicyProvider policyProvider,
    IConfigurationTrustOptionsProvider optionsProvider
) : IConfigurationTrustMonitor
{
    private static ConfigurationTrustDecision EnforcementDisabled(string path) => new
    (
        Path: path,
        IsTrusted: true,
        Decisions:
        [
            new ConfigurationTrustPolicyDecision("cyborg.trust.policy.disabled", ConfigurationTrustDecisionKind.Abstain, Reason: "Trust evaluation is disabled by configuration.")
        ]
    );

    public async ValueTask<ConfigurationTrustDecision> EvaluateAsync(string path, CancellationToken cancellationToken = default)
    {
        if (optionsProvider.Options.EnforcementMode is TrustEnforcementMode.Disabled)
        {
            return EnforcementDisabled(path);
        }
        ConfigurationTrustSubject subject = new(path);

        List<ConfigurationTrustPolicyDecision> decisions = [];
        foreach (IConfigurationTrustPolicy policy in policyProvider.GetPolicies())
        {
            ConfigurationTrustPolicyDecision decision = await policy.EvaluateAsync(serviceProvider, subject, cancellationToken);
            decisions.Add(decision);
            if (decision.Decision == ConfigurationTrustDecisionKind.Reject)
            {
                return new ConfigurationTrustDecision(path, IsTrusted: false, decisions);
            }
        }
        return new ConfigurationTrustDecision(path, IsTrusted: true, decisions);
    }
}