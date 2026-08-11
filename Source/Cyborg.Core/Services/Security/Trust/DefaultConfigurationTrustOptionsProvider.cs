using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Services.Security.Trust.Configuration;

namespace Cyborg.Core.Services.Security.Trust;

public sealed class DefaultConfigurationTrustOptionsProvider(IConfiguration configuration) : IConfigurationTrustOptionsProvider
{
    private const string POLICIES_KEY = "cyborg.services.trust.policies";
    private const string ENFORCEMENT_MODE_KEY = "cyborg.services.trust.enforcement_mode";

    public ConfigurationTrustOptions Options => field ??= CreateOptions();

    private ConfigurationTrustOptions CreateOptions()
    {
        ConfigurationTrustOptions defaults = ConfigurationTrustOptions.Default;
        IReadOnlyList<DynamicValue> policies = configuration.Get(POLICIES_KEY, defaults.Policies);
        TrustEnforcementMode enforcementMode = configuration.Get(ENFORCEMENT_MODE_KEY, defaults.EnforcementMode);
        return defaults with
        {
            Policies = policies,
            EnforcementMode = enforcementMode,
        };
    }
}
