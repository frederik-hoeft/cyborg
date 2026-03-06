using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Aot.Modules.Loaders.Configuration;

namespace Cyborg.Modules.Named;

[GeneratedModuleLoaderFactory]
public sealed partial class NamedModuleReferenceModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<NamedModuleReferenceModuleWorker, NamedModuleReferenceModule>(serviceProvider);