using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed class ModuleReferenceDynamicProvider() : DynamicValueProviderBase<ModuleReference>("cyborg.types.module.reference.v1");
