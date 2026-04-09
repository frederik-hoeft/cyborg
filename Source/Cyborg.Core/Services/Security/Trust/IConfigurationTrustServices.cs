using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Cyborg.Core.Services.Security.Trust.Policies;
using Jab;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Services.Security.Trust;

[ServiceProviderModule]
[Singleton<IDynamicValueProvider, ConfigurationTrustOptionsDynamicProvider>]
[Singleton<IDynamicValueProvider, UnixPermissionsTrustPolicyDynamicProvider>]
[Singleton<IDynamicValueProvider, UnixOwnerTrustPolicyDynamicProvider>]
[Singleton<IConfigurationTrustMonitor, UnionConfigurationTrustMonitor>]
[Singleton<IConfigurationTrustPolicyProvider, DefaultConfigurationTrustPolicyProvider>]
[Singleton<IConfigurationTrustOptionsProvider, DefaultConfigurationTrustOptionsProvider>]
[Singleton<IConfigurationTrustService, DefaultConfigurationTrustService>]
[Singleton<JsonConverter>(Factory = nameof(CreateTrustEnforcementModeConverter))]
[Singleton<JsonConverter>(Factory = nameof(CreateUnixFileModeConverter))]
[Singleton<JsonSerializerContext>(Factory = nameof(GetConfigurationTrustJsonSerializerContext))]
public interface IConfigurationTrustServices
{
    static JsonConverter CreateTrustEnforcementModeConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<TrustEnforcementMode>(namingPolicy);

    static JsonConverter CreateUnixFileModeConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<UnixFileMode>(namingPolicy);

    static ConfigurationTrustJsonSerializerContext GetConfigurationTrustJsonSerializerContext() => ConfigurationTrustJsonSerializerContext.Default;
}