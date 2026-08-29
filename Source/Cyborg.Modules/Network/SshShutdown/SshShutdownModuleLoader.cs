using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Network.SshShutdown;

[GeneratedModuleLoaderFactory]
public sealed partial class SshShutdownModuleLoader : ModuleLoader<SshShutdownModuleWorker, SshShutdownModule>;
