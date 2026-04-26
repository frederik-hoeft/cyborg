using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Configuration.ConfigCollection;

[GeneratedModuleLoaderFactory]
public sealed partial class ConfigCollectionModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<ConfigCollectionModuleWorker, ConfigCollectionModule>(serviceProvider);
