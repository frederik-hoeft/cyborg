using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Conditions.IsSet;

public sealed class IsSetModuleWorker(IWorkerContext<IsSetModule> context) : ModuleWorker<IsSetModule>(context)
{
    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        bool value = runtime.Environment.TryResolveVariable(Module.Variable, out object? _);
        return Task.FromResult(runtime.Exit(Success(new ConditionalResult(value))));
    }
}
