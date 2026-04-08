using Cyborg.Core.Services.Security.Trust.Policies;

namespace Cyborg.Core.Services.Security.Trust;

public sealed class UnionConfigurationTrustMonitor(IConfigurationTrustPolicyProvider policyProvider) : IConfigurationTrustMonitor
{
    public async ValueTask<ConfigurationTrustDecision> EvaluateAsync(string path, CancellationToken cancellationToken = default)
    {
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