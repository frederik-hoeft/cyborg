using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Dynamic;

[GeneratedModuleLoaderFactory]
public sealed partial class DynamicModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<DynamicModuleWorker, DynamicModule>(serviceProvider);