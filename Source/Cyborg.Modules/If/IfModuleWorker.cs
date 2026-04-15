using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Modules.Conditions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.If;

public sealed class IfModuleWorker(IWorkerContext<IfModule> context) : ConditionalModuleWorkerBase<IfModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        PathSyntax childNamespace = runtime.Environment.SyntaxFactory.Path(runtime.Environment.Namespace);
        IRuntimeEnvironment environment = CreateChildEnvironment(runtime, Module.Condition, childNamespace);
        string conditionModuleId = Module.Condition.Module.ModuleId;
        Logger.LogConditionEvaluating(conditionModuleId);
        IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Condition.Module, environment, cancellationToken);
        if (result.Status is ModuleExitStatus.Canceled)
        {
            return runtime.Exit(WithStatus(ModuleExitStatus.Canceled));
        }
        if (result.Status is not ModuleExitStatus.Success)
        {
            // Skipped is not a valid result for condition modules; treat all remaining non-success statuses as failure
            Logger.LogConditionFailed(conditionModuleId, result.Status.ToString());
            return runtime.Exit(WithStatus(ModuleExitStatus.Failed));
        }
        string resultVariable = childNamespace.Member(nameof(ConditionalResult.Result));
        if (!result.Artifacts.TryResolveVariable(resultVariable, out bool condition))
        {
            // this is not a valid result from the condition module
            Logger.LogConditionResultUnreadable(conditionModuleId);
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