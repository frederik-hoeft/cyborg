using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.If;

public sealed class IfModuleWorker(IWorkerContext<IfModule> context) : ModuleWorker<IfModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ModuleArtifacts childArtifacts = ModuleArtifacts.Default with
        {
            Namespace = runtime.Environment.Namespace,
            DecompositionStrategy = DecompositionStrategy.LeavesOnly,
            Environment = ArtifactModuleEnvironment.Default with { Scope = EnvironmentScope.Parent } // need artifacts to be accessible to us
        };
        // don't need a customizable environment for the if condition
        IRuntimeEnvironment environment = runtime.PrepareEnvironment(ModuleEnvironment.Default);
        // force if condition to write its artifacts to a known location in the parent environment so we can read it after execution
        // @<child_module_id>.artifacts via @ override of child property
        string artifactsOverride = environment.SyntaxFactory.Path(environment.NamespaceOf(Module.Condition)).Property(Module.Artifacts).Override();
        environment.SetVariable(artifactsOverride, childArtifacts);
        string conditionModuleId = Module.Condition.Module.ModuleId;
        Logger.LogIfConditionEvaluating(conditionModuleId);
        IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Condition.Module, environment, cancellationToken);
        if (result.Status is not ModuleExitStatus.Success)
        {
            // this was unexpected
            Logger.LogIfConditionFailed(conditionModuleId, result.Status.ToString());
            return runtime.Exit(WithStatus(result.Status));
        }
        // ${@}.result, via ${@} self reference
        string resultAccessExpression = environment.SyntaxFactory.Self().Ref().Member(nameof(ConditionalResult.Result));
        string resultVariable = runtime.Environment.Interpolate(resultAccessExpression);
        if (!result.Artifacts.TryResolveVariable(resultVariable, out bool condition))
        {
            // this is not a valid result from the condition module
            Logger.LogIfConditionResultUnreadable(conditionModuleId);
            return runtime.Exit(WithStatus(ModuleExitStatus.Failed));
        }
        ModuleContext? branchToExecute = condition != Module.InvertCondition ? Module.Then : Module.Else;
        if (branchToExecute is null)
        {
            Logger.LogIfNoBranch(condition);
            return runtime.Exit(WithStatus(ModuleExitStatus.Skipped));
        }
        Logger.LogIfBranchTaken(condition, condition ? "then" : "else");
        IModuleExecutionResult branchResult = await runtime.ExecuteAsync(branchToExecute, cancellationToken);
        return runtime.Exit(WithStatus(branchResult.Status));
    }
}