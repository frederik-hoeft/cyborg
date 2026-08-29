using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Empty;

[GeneratedModuleLoaderFactory]
public sealed partial class EmptyModuleLoader : ModuleLoader<EmptyModuleWorker, EmptyModule>;
