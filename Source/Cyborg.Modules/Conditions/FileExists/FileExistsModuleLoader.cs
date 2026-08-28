using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Runtime.Configuration;

namespace Cyborg.Modules.Conditions.FileExists;

[GeneratedModuleLoaderFactory]
public sealed partial class FileExistsModuleLoader : ModuleLoader<FileExistsModuleWorker, FileExistsModule>;
