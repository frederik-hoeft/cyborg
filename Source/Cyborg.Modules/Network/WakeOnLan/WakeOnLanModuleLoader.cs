using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Network.WakeOnLan;

[GeneratedModuleLoaderFactory]
public sealed partial class WakeOnLanModuleLoader : ModuleLoader<WakeOnLanModuleWorker, WakeOnLanModule>;
