using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Configuration.ExternalConfig;

[GeneratedModuleLoaderFactory]
public sealed partial class ExternalConfigModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<ExternalConfigModuleWorker, ExternalConfigModule>(serviceProvider);