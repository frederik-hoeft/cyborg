using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.TestModules.Activation;

[GeneratedModuleLoaderFactory]
public sealed partial class ActivationProbeModuleLoader : ModuleLoader<ActivationProbeModuleWorker, ActivationProbeModule>;
