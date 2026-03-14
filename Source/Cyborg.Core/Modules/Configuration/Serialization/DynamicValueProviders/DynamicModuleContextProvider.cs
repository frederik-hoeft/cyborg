using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Core.Modules.Configuration.Serialization.DynamicValueProviders;

public sealed class DynamicModuleContextProvider() : DynamicValueProviderBase<ModuleContext>("cyborg.types.module.context.v1");