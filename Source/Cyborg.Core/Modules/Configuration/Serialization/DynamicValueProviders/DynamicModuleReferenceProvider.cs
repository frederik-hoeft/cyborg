using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Configuration.Serialization.DynamicValueProviders;

public sealed class DynamicModuleReferenceProvider() : DynamicValueProviderBase<ModuleReference>("cyborg.types.module.reference.v1");