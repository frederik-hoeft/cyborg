using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Parallel;

[GeneratedModuleLoaderFactory]
public sealed partial class ParallelModuleLoader : ModuleLoader<ParallelModuleWorker, ParallelModule>;
