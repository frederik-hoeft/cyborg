using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Foreach;

[GeneratedModuleLoaderFactory]
public sealed partial class ForeachModuleLoader : ModuleLoader<ForeachModuleWorker, ForeachModule>;
