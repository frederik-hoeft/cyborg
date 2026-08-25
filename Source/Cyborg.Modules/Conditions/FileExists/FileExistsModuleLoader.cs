using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Conditions.FileExists;

[GeneratedModuleLoaderFactory]
public sealed partial class FileExistsModuleLoader : ModuleLoader<FileExistsModuleWorker, FileExistsModule>;
