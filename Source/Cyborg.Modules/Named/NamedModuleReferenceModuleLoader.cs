using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Named;

[GeneratedModuleLoaderFactory]
public sealed partial class NamedModuleReferenceModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<NamedModuleReferenceModuleWorker, NamedModuleReferenceModule>(serviceProvider);
