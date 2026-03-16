using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Configuration.Serialization;
using Cyborg.Modules.Borg.Compact;
using Cyborg.Modules.Borg.Create;
using Cyborg.Modules.Borg.Model;
using Cyborg.Modules.Borg.Prune;
using Jab;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Modules.Borg;

[ServiceProviderModule]
[Singleton<BorgJsonSerializerContext>(Factory = nameof(GetBorgJsonSerializerContext))]
[Singleton<JsonSerializerContext>(Factory = nameof(GetBorgJsonSerializerContext))]
[Singleton<JsonNamingPolicy>(Factory = nameof(GetModuleJsonNamingPolicy))]
[Singleton<IDynamicValueProvider, BorgRemoteValueProvider>]
[Singleton<IModuleLoader, BorgCreateModuleLoader>]
[Singleton<IModuleLoader, BorgPruneModuleLoader>]
[Singleton<IModuleLoader, BorgCompactModuleLoader>]
[Singleton<JsonConverter>(Factory = nameof(CreateEnvironmentScopeReferenceConverter))]
public interface ICyborgBorgServices
{
    static BorgJsonSerializerContext GetBorgJsonSerializerContext() => BorgJsonSerializerContext.Default;

    static JsonNamingPolicy GetModuleJsonNamingPolicy() => JsonNamingPolicy.SnakeCaseLower;

    static JsonConverter CreateEnvironmentScopeReferenceConverter(JsonNamingPolicy namingPolicy) => new JsonStringEnumConverter<EnvironmentScopeReference>(namingPolicy);
}
