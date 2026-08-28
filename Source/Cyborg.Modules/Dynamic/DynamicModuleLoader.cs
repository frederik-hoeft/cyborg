using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Dynamic;

[GeneratedModuleLoaderFactory]
public sealed partial class DynamicModuleLoader : ModuleLoader<DynamicModuleWorker, DynamicModule>;
