using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Configuration.ConfigMap;

[GeneratedModuleLoaderFactory]
public sealed partial class ConfigMapModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<ConfigMapModuleWorker, ConfigMapModule>(serviceProvider);