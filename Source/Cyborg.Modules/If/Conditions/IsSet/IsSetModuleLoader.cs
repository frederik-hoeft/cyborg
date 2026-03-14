using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.If.Conditions.IsSet;

[GeneratedModuleLoaderFactory]
public sealed partial class IsSetModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<IsSetModuleWorker, IsSetModule>(serviceProvider);