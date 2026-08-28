using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Runtime.Model;

public sealed class ModuleEnvironmentDynamicProvider() : DynamicValueProviderBase<ModuleEnvironment>("cyborg.types.module.environment.v1");
