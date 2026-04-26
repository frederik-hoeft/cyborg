using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Switch;

[GeneratedModuleLoaderFactory]
public sealed partial class SwitchModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<SwitchModuleWorker, SwitchModule>(serviceProvider);
