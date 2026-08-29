using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Assert;

[GeneratedModuleLoaderFactory]
public sealed partial class AssertModuleLoader : ModuleLoader<AssertModuleWorker, AssertModule>;
