using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.External;

[GeneratedModuleLoaderFactory]
public sealed partial class ExternalModuleLoader : ModuleLoader<ExternalModuleWorker, ExternalModule>;
