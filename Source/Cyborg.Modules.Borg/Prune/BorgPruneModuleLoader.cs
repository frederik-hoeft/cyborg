using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Borg.Prune;

[GeneratedModuleLoaderFactory]
public sealed partial class BorgPruneModuleLoader : ModuleLoader<BorgPruneModuleWorker, BorgPruneModule>;
