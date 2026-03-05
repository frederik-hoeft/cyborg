using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Aot.Modules.Loaders.Configuration;

namespace Cyborg.Modules.Configuration.ConfigCollection;

[GeneratedModuleLoaderFactory]
public sealed partial class ConfigCollectionModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<ConfigCollectionModuleWorker, ConfigCollectionModule>(serviceProvider);