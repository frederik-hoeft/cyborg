using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Sidecar;

[GeneratedModuleLoaderFactory]
public sealed partial class SidecarModuleLoader : ModuleLoader<SidecarModuleWorker, SidecarModule>;
