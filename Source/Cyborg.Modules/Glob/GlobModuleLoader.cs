using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Glob;

[GeneratedModuleLoaderFactory]
public sealed partial class GlobModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<GlobModuleWorker, GlobModule>(serviceProvider);
