using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Conditions.Or;

[GeneratedModuleLoaderFactory]
public sealed partial class OrModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<OrModuleWorker, OrModule>(serviceProvider);
