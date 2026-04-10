using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.While;

[GeneratedModuleLoaderFactory]
public sealed partial class WhileModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<WhileModuleWorker, WhileModule>(serviceProvider);
