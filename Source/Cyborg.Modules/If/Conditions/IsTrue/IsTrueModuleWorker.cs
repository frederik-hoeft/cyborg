using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.If.Conditions.IsTrue;

public sealed class IsTrueModuleWorker(IWorkerContext<IsTrueModule> context) : ModuleWorker<IsTrueModule>(context)
{
    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (!runtime.Environment.TryResolveVariable(Module.Variable, out bool value))
        {
            // undefined variable or type mismatch
            return Task.FromResult(runtime.Exit(Failed()));
        }
        return Task.FromResult(runtime.Exit(Success(new ConditionalResult(value))));
    }
}
