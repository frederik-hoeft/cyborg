using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Conditions.FileExists;

[GeneratedModuleLoaderFactory]
public sealed partial class FileExistsModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<FileExistsModuleWorker, FileExistsModule>(serviceProvider);
