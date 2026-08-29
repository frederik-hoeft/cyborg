using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Configuration.ExternalConfig;

[GeneratedModuleLoaderFactory]
public sealed partial class ExternalConfigModuleLoader : ModuleLoader<ExternalConfigModuleWorker, ExternalConfigModule>;
