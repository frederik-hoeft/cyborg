using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.If.Conditions.IsTrue;

[GeneratedModuleLoaderFactory]
public sealed partial class IsTrueModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<IsTrueModuleWorker, IsTrueModule>(serviceProvider);