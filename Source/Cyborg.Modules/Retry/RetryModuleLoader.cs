using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Retry;

[GeneratedModuleLoaderFactory]
public sealed partial class RetryModuleLoader : ModuleLoader<RetryModuleWorker, RetryModule>;
