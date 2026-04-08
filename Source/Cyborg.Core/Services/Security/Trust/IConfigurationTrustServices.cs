using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Jab;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Services.Security.Trust;

[ServiceProviderModule]
[Singleton<IDynamicValueProvider, ConfigurationTrustOptionsDynamicProvider>]
[Singleton<IConfigurationTrustMonitor, UnionConfigurationTrustMonitor>]
[Singleton<IConfigurationTrustPolicyProvider, DefaultConfigurationTrustPolicyProvider>]
[Singleton<IConfigurationTrustOptionsProvider, DefaultConfigurationTrustOptionsProvider>]
[Singleton<JsonConverter>(Factory = nameof(CreateTrustEnforcementModeConverter))]
public interface IConfigurationTrustServices
{
    static JsonConverter CreateTrustEnforcementModeConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<TrustEnforcementMode>(namingPolicy);
}