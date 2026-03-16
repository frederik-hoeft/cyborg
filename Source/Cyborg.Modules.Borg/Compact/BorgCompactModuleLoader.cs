using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Borg.Compact;

[GeneratedModuleLoaderFactory]
public sealed partial class BorgCompactModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<BorgCompactModuleWorker, BorgCompactModule>(serviceProvider);