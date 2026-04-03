using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.If.Conditions.DirectoryExists;

[GeneratedModuleLoaderFactory]
public sealed partial class DirectoryExistsModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<DirectoryExistsModuleWorker, DirectoryExistsModule>(serviceProvider);