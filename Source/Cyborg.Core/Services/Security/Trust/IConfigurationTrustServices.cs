using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Services.IO;
using Cyborg.Core.Services.Security.Trust.Configuration;
using Cyborg.Core.Services.Security.Trust.Policies;
using Jab;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Services.Security.Trust;

[ServiceProviderModule]
[Singleton<IPathCanonicalizationService, DefaultPathCanonicalizationService>]
[Singleton<IDynamicValueProvider, ConfigurationTrustOptionsDynamicProvider>]
[Singleton<IDynamicValueProvider, UnixPermissionsTrustPolicyDynamicProvider>]
[Singleton<IDynamicValueProvider, UnixOwnerTrustPolicyDynamicProvider>]
[Singleton<IConfigurationTrustMonitor, UnionConfigurationTrustMonitor>]
[Singleton<IConfigurationTrustPolicyProvider, DefaultConfigurationTrustPolicyProvider>]
[Singleton<IConfigurationTrustOptionsProvider, DefaultConfigurationTrustOptionsProvider>]
[Singleton<IConfigurationTrustService, DefaultConfigurationTrustService>]
[Singleton<JsonConverter>(Factory = nameof(CreateTrustEnforcementModeConverter))]
[Singleton<JsonConverter>(Factory = nameof(CreateUnixFileModeConverter))]
[Singleton<JsonConverter>(Factory = nameof(CreateConfigurationTrustDecisionKindConverter))]
[Singleton<JsonSerializerContext>(Factory = nameof(GetConfigurationTrustJsonSerializerContext))]
public interface IConfigurationTrustServices
{
    static JsonConverter CreateTrustEnforcementModeConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<TrustEnforcementMode>(namingPolicy);

    static JsonConverter CreateUnixFileModeConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<UnixFileMode>(namingPolicy);

    static JsonConverter CreateConfigurationTrustDecisionKindConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<ConfigurationTrustDecisionKind>(namingPolicy);

    static ConfigurationTrustJsonSerializerContext GetConfigurationTrustJsonSerializerContext() => ConfigurationTrustJsonSerializerContext.Default;
}
