using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

public sealed class DynamicModuleContextProvider() : DynamicValueProviderBase<ModuleContext>("cyborg.types.module.context.v1");