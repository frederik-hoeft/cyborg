using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.If;

[GeneratedModuleLoaderFactory]
public sealed partial class IfModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<IfModuleWorker, IfModule>(serviceProvider);