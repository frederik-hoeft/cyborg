using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed class ModuleEnvironmentDynamicProvider() : DynamicValueProviderBase<ModuleEnvironment>("cyborg.types.module.environment.v1");
