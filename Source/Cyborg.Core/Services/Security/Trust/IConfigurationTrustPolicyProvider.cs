using Cyborg.Core.Services.Security.Trust.Policies;

namespace Cyborg.Core.Services.Security.Trust;

public interface IConfigurationTrustPolicyProvider
{
    IEnumerable<IConfigurationTrustPolicy> GetPolicies();
}