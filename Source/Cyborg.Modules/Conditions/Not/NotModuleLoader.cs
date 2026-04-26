using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Conditions.Not;

[GeneratedModuleLoaderFactory]
public sealed partial class NotModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<NotModuleWorker, NotModule>(serviceProvider);
