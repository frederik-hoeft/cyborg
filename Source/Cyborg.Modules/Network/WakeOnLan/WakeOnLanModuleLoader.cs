using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Network.WakeOnLan;

[GeneratedModuleLoaderFactory]
public sealed partial class WakeOnLanModuleLoader : ModuleLoader<WakeOnLanModuleWorker, WakeOnLanModule>;
