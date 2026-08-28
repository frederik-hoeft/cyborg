using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.TestModules.Activation;

[GeneratedModuleLoaderFactory]
public sealed partial class ActivationProbeModuleLoader : ModuleLoader<ActivationProbeModuleWorker, ActivationProbeModule>;
