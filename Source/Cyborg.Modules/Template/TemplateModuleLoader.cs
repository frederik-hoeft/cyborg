using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.Template;

[GeneratedModuleLoaderFactory]
public sealed partial class TemplateModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<TemplateModuleWorker, TemplateModule>(serviceProvider);
