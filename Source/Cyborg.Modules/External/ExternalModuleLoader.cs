using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.External;

[GeneratedModuleLoaderFactory]
public sealed partial class ExternalModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<ExternalModuleWorker, ExternalModule>(serviceProvider);