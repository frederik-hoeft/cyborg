using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Subprocess;

[GeneratedModuleLoaderFactory]
public sealed partial class SubprocessModuleLoader : ModuleLoader<SubprocessModuleWorker, SubprocessModule>;
