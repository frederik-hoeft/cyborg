using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Configuration.Serialization;
using Cyborg.Modules.Borg.Compact;
using Cyborg.Modules.Borg.Create;
using Cyborg.Modules.Borg.Prune;
using Cyborg.Modules.Borg.Shared.Json;
using Cyborg.Modules.Borg.Shared.Model;
using Cyborg.Modules.Borg.Shared.Output;
using Cyborg.Modules.Borg.Shared.Output.Deserializers;
using Jab;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules.Borg;

[ServiceProviderModule]
[Singleton<BorgJsonSerializerContext>(Factory = nameof(GetBorgJsonSerializerContext))]
[Singleton<JsonSerializerContext>(Factory = nameof(GetBorgJsonSerializerContext))]
[Singleton<BorgOutputJsonSerializerContext>(Factory = nameof(GetBorgOutputJsonSerializerContext))]
[Singleton<JsonNamingPolicy>(Factory = nameof(GetModuleJsonNamingPolicy))]
[Singleton<IDynamicValueProvider, BorgRemoteValueProvider>]
[Singleton<IDynamicValueProvider, BorgRemoteRepositoryValueProvider>]
[Singleton<IModuleLoader, BorgCreateModuleLoader>]
[Singleton<IModuleLoader, BorgPruneModuleLoader>]
[Singleton<IModuleLoader, BorgCompactModuleLoader>]
[Singleton<IBorgOutputDeserializer, BorgLogMessageDeserializer>]
[Singleton<IBorgOutputDeserializerRegistry, BorgOutputDeserializerRegistry>]
[Singleton<IBorgOutputLineParser, BorgOutputLineParser>]
[Singleton<JsonConverter>(Factory = nameof(CreateEnvironmentScopeReferenceConverter))]
public interface ICyborgBorgServices
{
    static BorgJsonSerializerContext GetBorgJsonSerializerContext() => BorgJsonSerializerContext.Default;

    static BorgOutputJsonSerializerContext GetBorgOutputJsonSerializerContext() => BorgOutputJsonSerializerContext.Default;

    static JsonNamingPolicy GetModuleJsonNamingPolicy() => JsonNamingPolicy.SnakeCaseLower;

    static JsonConverter CreateEnvironmentScopeReferenceConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<EnvironmentScopeReference>(namingPolicy);
}
