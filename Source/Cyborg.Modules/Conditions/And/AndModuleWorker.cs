using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Conditions.And;

public sealed class AndModuleWorker(IWorkerContext<AndModule> context) : ConditionalCombinatorModuleWorkerBase<AndModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        int index = 0;
        foreach (ModuleReference condition in Module.Conditions)
        {
            PathSyntax childNamespace = runtime.Environment.SyntaxFactory.Path(runtime.Environment.Namespace).Child(index.ToString());
            IRuntimeEnvironment environment = CreateChildEnvironment(runtime, condition, childNamespace);
            string conditionModuleId = condition.Module.ModuleId;
            IModuleExecutionResult result = await runtime.ExecuteAsync(condition.Module, environment, cancellationToken);
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
            if (!result.Artifacts.TryResolveVariable(resultVariable, out bool conditionalResult))
            {
                // this is not a valid result from the condition module
                Logger.LogConditionResultUnreadable(conditionModuleId);
                return runtime.Exit(WithStatus(ModuleExitStatus.Failed));
            }
            if (!conditionalResult)
            {
                return runtime.Exit(Success(new ConditionalResult(false)));
            }
            ++index;
        }
        return runtime.Exit(Success(new ConditionalResult(true)));
    }
}