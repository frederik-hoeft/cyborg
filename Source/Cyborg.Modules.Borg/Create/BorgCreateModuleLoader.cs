using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Borg.Create;

[GeneratedModuleLoaderFactory]
public sealed partial class BorgCreateModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<BorgCreateModuleWorker, BorgCreateModule>(serviceProvider);