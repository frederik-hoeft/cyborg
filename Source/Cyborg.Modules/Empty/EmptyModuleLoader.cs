using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Empty;

[GeneratedModuleLoaderFactory]
public sealed partial class EmptyModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<EmptyModuleWorker, EmptyModule>(serviceProvider);
