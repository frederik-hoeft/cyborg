using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Configuration.Serialization.DynamicValueProviders;

public sealed class DynamicModuleEnvironmentProvider() : DynamicValueProviderBase<ModuleEnvironment>("cyborg.types.module.environment.v1");
