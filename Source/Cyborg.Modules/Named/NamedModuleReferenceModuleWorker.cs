using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Named;

public sealed class NamedModuleReferenceModuleWorker(IWorkerContext<NamedModuleReferenceModule> context, IModuleRegistry moduleRegistry) : ModuleWorker<NamedModuleReferenceModule>(context)
{
    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (!moduleRegistry.TryGetModule(Module.Target, out ModuleContext? targetModule))
        {
            throw new InvalidOperationException($"Failed to find target module with name '{Module.Target}'.");
        }
        return runtime.ExecuteAsync(targetModule, cancellationToken);
    }
}
