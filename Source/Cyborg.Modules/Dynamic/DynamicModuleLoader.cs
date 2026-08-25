using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Dynamic;

[GeneratedModuleLoaderFactory]
public sealed partial class DynamicModuleLoader : ModuleLoader<DynamicModuleWorker, DynamicModule>;
