using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Modules.Conditions;

public abstract class ConditionalCombinatorModuleWorkerBase<TModule>(IWorkerContext<TModule> context) : ConditionalModuleWorkerBase<TModule>(context) where TModule : ModuleBase, IModule<TModule>
{
    protected async Task<ChildExecutionResult> ExecuteConditionAsync(IModuleRuntime runtime, ModuleReference condition, PathSyntax conditionNamespace, CancellationToken cancellationToken)
    {
        IRuntimeEnvironment environment = CreateChildEnvironment(runtime, condition, conditionNamespace);
        string conditionModuleId = condition.ModuleId;
        IModuleExecutionResult result = await runtime.ExecuteAsync(condition, environment, cancellationToken);
        if (result.Status is ModuleExitStatus.Canceled)
        {
            return ChildExecutionResult.NonSuccess(ModuleExitStatus.Canceled);
        }
        if (result.Status is not ModuleExitStatus.Success)
        {
            // Skipped is not a valid result for condition modules; treat all remaining non-success statuses as failure
            Logger.LogConditionFailed(conditionModuleId, result.Status.ToString());
            return ChildExecutionResult.NonSuccess(ModuleExitStatus.Failed);
        }
        string resultVariable = conditionNamespace.Member(nameof(ConditionalResult.Result));
        if (!result.Artifacts.TryResolveVariable(resultVariable, out bool conditionalResult))
        {
            // this is not a valid result from the condition module
            Logger.LogConditionResultUnreadable(conditionModuleId);
            return ChildExecutionResult.NonSuccess(ModuleExitStatus.Failed);
        }
        return ChildExecutionResult.Success(conditionalResult);
    }

    protected readonly record struct ChildExecutionResult(bool ConditionResult, ModuleExitStatus Status)
    {
        public static ChildExecutionResult Success(bool conditionalResult) => new(conditionalResult, ModuleExitStatus.Success);

        public static ChildExecutionResult NonSuccess(ModuleExitStatus status)
        {
            if (status is ModuleExitStatus.Success)
            {
                throw new ArgumentException("Status must be a non-success value", nameof(status));
            }
            return new ChildExecutionResult(default, status);
        }
    }
}
