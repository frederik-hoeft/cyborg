using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Guard;

[GeneratedModuleLoaderFactory]
public sealed partial class GuardModuleLoader : ModuleLoader<GuardModuleWorker, GuardModule>;
