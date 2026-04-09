using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Core.Modules.Configuration.Model;

public sealed class ModuleContextDynamicProvider() : DynamicValueProviderBase<ModuleContext>("cyborg.types.module.context.v1");