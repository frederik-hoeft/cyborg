using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules.Configuration;

namespace Cyborg.Modules.EnvironmentDefinitions;

[GeneratedModuleLoaderFactory]
public sealed partial class EnvironmentDefinitionsModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<EnvironmentDefinitionsModuleWorker, EnvironmentDefinitionsModule>(serviceProvider);