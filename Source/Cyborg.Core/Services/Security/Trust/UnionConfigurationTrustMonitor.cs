using Cyborg.Core.Services.Security.Trust.Configuration;
using Cyborg.Core.Services.Security.Trust.Policies;

namespace Cyborg.Core.Services.Security.Trust;

public sealed class UnionConfigurationTrustMonitor(IConfigurationTrustPolicyProvider policyProvider, IConfigurationTrustOptionsProvider optionsProvider) : IConfigurationTrustMonitor
{
    private static ConfigurationTrustDecision EnforcementDisabledDecision => field ??= new
    (
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
            return EnforcementDisabledDecision;
        }
        ConfigurationTrustSubject subject = new(path);

        List<ConfigurationTrustPolicyDecision> decisions = [];
        foreach (IConfigurationTrustPolicy policy in policyProvider.GetPolicies())
        {
            ConfigurationTrustPolicyDecision decision = await policy.EvaluateAsync(subject, cancellationToken);
            decisions.Add(decision);
            if (decision.Decision == ConfigurationTrustDecisionKind.Reject)
            {
                return new ConfigurationTrustDecision(IsTrusted: false, decisions);
            }
        }
        return new ConfigurationTrustDecision(IsTrusted: true, decisions);
    }
}