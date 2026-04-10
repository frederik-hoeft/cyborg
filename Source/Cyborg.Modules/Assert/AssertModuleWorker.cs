using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Modules.Conditions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Assert;

public sealed class AssertModuleWorker(IWorkerContext<AssertModule> context) : ConditionalModuleWorkerBase<AssertModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        PathSyntax childNamespace = runtime.Environment.SyntaxFactory.Path(runtime.Environment.Namespace);
        IRuntimeEnvironment environment = CreateChildEnvironment(runtime, Module.Assertion, childNamespace);
        IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Assertion.Module, environment, cancellationToken);
        if (result.Status is not ModuleExitStatus.Success)
        {
            // this was unexpected
            return runtime.Exit(WithStatus(result.Status));
        }
        string resultVariable = childNamespace.Member(nameof(ConditionalResult.Result));
        if (!result.Artifacts.TryResolveVariable(resultVariable, out bool condition))
        {
            // this is not a valid result from the condition module
            return runtime.Exit(WithStatus(ModuleExitStatus.Failed));
        }
        if (!condition)
        {
            string message = runtime.Environment.Interpolate(Module.Message);
            AssertModuleResult failure = new(message);
            return runtime.Exit(Failed(failure));
        }
        return runtime.Exit(Success());
    }
}