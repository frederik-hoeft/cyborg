using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Subprocess;

[GeneratedModuleLoaderFactory]
public sealed partial class SubprocessModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<SubprocessModuleWorker, SubprocessModule>(serviceProvider);
