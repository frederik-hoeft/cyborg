using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.If.Conditions.FileExists;

[GeneratedModuleLoaderFactory]
public sealed partial class FileExistsModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<FileExistsModuleWorker, FileExistsModule>(serviceProvider);