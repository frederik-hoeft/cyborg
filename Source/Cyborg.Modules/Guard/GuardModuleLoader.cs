using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Guard;

[GeneratedModuleLoaderFactory]
public sealed partial class GuardModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<GuardModuleWorker, GuardModule>(serviceProvider);