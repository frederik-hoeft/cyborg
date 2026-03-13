using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.If;

public sealed class IfModuleWorker(IWorkerContext<IfModule> context) : ModuleWorker<IfModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ModuleArtifacts childArtifacts = ModuleArtifacts.Default with
        {
            Namespace = runtime.Environment.EffectiveNamespace,
            DecompositionStrategy = DecompositionStrategy.LeavesOnly,
            Environment = ModuleEnvironment.Default
        };
        VariableSyntaxFactory parentSyntax = runtime.Environment.SyntaxFactory;
        IRuntimeEnvironment environment = runtime.PrepareEnvironment(new ModuleEnvironment(EnvironmentScope.InheritParent, Name: null));
        environment.SetVariable(parentSyntax.Override(parentSyntax.Namespace(environment.GetEffectiveNamespace(Module.Condition.Module)).Combine(parentSyntax.MemberVariable(nameof(Module.Artifacts)))).Render(), childArtifacts);
        IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Condition.Module, environment, cancellationToken);
        if (result.Status is not ModuleExitStatus.Success)
        {
            // this was unexpected
            return runtime.Exit(WithStatus(result.Status));
        }
        if (!result.Artifacts.TryGetValue(runtime.Environment.Interpolate(parentSyntax.Variable(parentSyntax.Ref(parentSyntax.Self()).Render(), "result").Render()), out object? value) || value is not bool condition)
        {
            // this is not a valid result from the condition module
            return runtime.Exit(WithStatus(ModuleExitStatus.Failed));
        }
        ModuleContext branchToExecute = condition ? Module.Then : Module.Else ?? Module.Then;
        IModuleExecutionResult branchResult = await runtime.ExecuteAsync(branchToExecute, cancellationToken);
        return runtime.Exit(WithStatus(branchResult.Status));
    }
}