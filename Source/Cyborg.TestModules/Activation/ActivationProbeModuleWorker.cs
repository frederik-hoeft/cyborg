using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.TestModules.Activation;

public sealed class ActivationProbeModuleWorker(IWorkerContext<ActivationProbeModule> context, ActivationProbeDependency dependency) : ModuleWorker<ActivationProbeModule>(context)
{
    public ActivationProbeDependency Dependency { get; } = dependency;

    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken) => Task.FromResult(runtime.Exit(Success()));
}
