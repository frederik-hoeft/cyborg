using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Assert;

[GeneratedModuleLoaderFactory]
public sealed partial class AssertModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<AssertModuleWorker, AssertModule>(serviceProvider);
