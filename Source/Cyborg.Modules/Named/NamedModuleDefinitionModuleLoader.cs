using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Modules.Named;

[GeneratedModuleLoaderFactory(Name = nameof(CreateWorkerCore))]
public sealed partial class NamedModuleDefinitionModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<NamedModuleDefinitionModuleWorker, NamedModuleDefinitionModule>(serviceProvider)
{
    private partial NamedModuleDefinitionModuleWorker CreateWorkerCore(NamedModuleDefinitionModule module, IServiceProvider serviceProvider);

    protected override NamedModuleDefinitionModuleWorker CreateWorker(NamedModuleDefinitionModule module, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(module);
        IModuleRegistry moduleRegistry = serviceProvider.GetRequiredService<IModuleRegistry>();
        if (!moduleRegistry.TryAddModule(module.Name, module))
        {
            throw new InvalidOperationException($"Failed to add module with name '{module.Name}' to the module registry. A module with the same name may already exist.");
        }
        return CreateWorkerCore(module, serviceProvider);
    }
}
