using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

public sealed class ModuleReferenceDynamicProvider() : DynamicValueProviderBase<ModuleReference>("cyborg.types.module.reference.v1");