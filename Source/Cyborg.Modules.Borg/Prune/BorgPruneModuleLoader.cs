using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Borg.Prune;

[GeneratedModuleLoaderFactory]
public sealed partial class BorgPruneModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<BorgPruneModuleWorker, BorgPruneModule>(serviceProvider);