using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Foreach;

[GeneratedModuleLoaderFactory]
public sealed partial class ForeachModuleLoader : ModuleLoader<ForeachModuleWorker, ForeachModule>;
