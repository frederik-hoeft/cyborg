using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Modules.Conditions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Assert;

public sealed class AssertModuleWorker(IWorkerContext<AssertModule> context) : ModuleWorker<AssertModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ModuleArtifacts childArtifacts = ModuleArtifacts.Default with
        {
            Namespace = runtime.Environment.Namespace,
            DecompositionStrategy = DecompositionStrategy.LeavesOnly,
            Environment = ArtifactModuleEnvironment.Default with { Scope = EnvironmentScope.Parent } // need artifacts to be accessible to us
        };
        // don't need a customizable environment for the condition
        IRuntimeEnvironment environment = runtime.PrepareEnvironment(ModuleEnvironment.Default);
        // force if condition to write its artifacts to a known location in the parent environment so we can read it after execution
        // @<child_module_id>.artifacts via @ override of child property
        string artifactsOverride = environment.SyntaxFactory.Path(environment.NamespaceOf(Module.Assertion)).Property(Module.Artifacts).Override();
        environment.SetVariable(artifactsOverride, childArtifacts);
        IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Assertion.Module, environment, cancellationToken);
        if (result.Status is not ModuleExitStatus.Success)
        {
            // this was unexpected
            return runtime.Exit(WithStatus(result.Status));
        }
        // ${@}.result, via ${@} self reference
        string resultAccessExpression = environment.SyntaxFactory.Self().Ref().Member(nameof(ConditionalResult.Result));
        string resultVariable = runtime.Environment.Interpolate(resultAccessExpression);
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