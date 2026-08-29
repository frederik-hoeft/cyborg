using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Borg.Compact;

[GeneratedModuleLoaderFactory]
public sealed partial class BorgCompactModuleLoader : ModuleLoader<BorgCompactModuleWorker, BorgCompactModule>;
