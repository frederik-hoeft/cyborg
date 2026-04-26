using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Services.Security.Trust.Policies;

namespace Cyborg.Core.Services.Security.Trust;

public sealed class DefaultConfigurationTrustPolicyProvider(IConfigurationTrustOptionsProvider optionsProvider) : IConfigurationTrustPolicyProvider
{
    private List<IConfigurationTrustPolicy>? _policies;

    public IEnumerable<IConfigurationTrustPolicy> GetPolicies()
    {
        if (_policies is null)
        {
            List<IConfigurationTrustPolicy> policies = [];
            foreach (DynamicValue policyContainer in optionsProvider.Options.Policies)
            {
                if (policyContainer.Value is not IConfigurationTrustPolicy policy)
                {
                    throw new InvalidOperationException($"Object of type {policyContainer.Value.GetType().FullName} is not a valid configuration trust policy.");
                }
                policies.Add(policy);
            }
            _policies = policies;
        }
        return _policies;
    }
}
