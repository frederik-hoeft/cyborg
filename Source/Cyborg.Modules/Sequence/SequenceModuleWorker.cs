using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environements;
using Cyborg.Modules.Shared.Model;

namespace Cyborg.Modules.Sequence;

// sample sequence module worker that executes each step in order and returns false if any step fails
public sealed class SequenceModuleWorker(SequenceModule module) : ModuleWorker<SequenceModule>(module)
{
    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        foreach (ModuleWithEnvironment step in Module.Steps)
        {
            IRuntimeEnvironment? environment = null;
            if (step.Environment?.Scope is EnvironmentScope.Reference)
            {
                if (string.IsNullOrEmpty(step.Environment.Name))
                {
                    throw new InvalidOperationException("Attempting to reference an environment without providing an environment name.");
                }
                if (!runtime.TryGetEnvironment(step.Environment.Name, out environment))
                {
                    throw new InvalidOperationException($"Attempting to reference an environment that does not exist: {step.Environment.Name}");
                }
            }
            EnvironmentScope scope = step.Environment?.Scope ?? EnvironmentScope.Isolated;
            bool success = await (environment switch
            {
                null => runtime.ExecuteAsync(step.Module.Module, scope, step.Environment?.Name, cancellationToken),
                _ => runtime.ExecuteAsync(step.Module.Module, environment, cancellationToken)
            });
            if (!success)
            {
                return false;
            }
        }
        return true;
    }
}
