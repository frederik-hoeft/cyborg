using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Conditions.IsSet;

[GeneratedModuleLoaderFactory]
public sealed partial class IsSetModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<IsSetModuleWorker, IsSetModule>(serviceProvider);
