using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Conditions.And;

[GeneratedModuleLoaderFactory]
public sealed partial class AndModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<AndModuleWorker, AndModule>(serviceProvider);