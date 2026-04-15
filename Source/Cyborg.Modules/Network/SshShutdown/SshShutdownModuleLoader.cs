using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Network.SshShutdown;

[GeneratedModuleLoaderFactory]
public sealed partial class SshShutdownModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<SshShutdownModuleWorker, SshShutdownModule>(serviceProvider);
