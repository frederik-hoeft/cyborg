using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Conditions.IsTrue;

[GeneratedModuleLoaderFactory]
public sealed partial class IsTrueModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<IsTrueModuleWorker, IsTrueModule>(serviceProvider);
