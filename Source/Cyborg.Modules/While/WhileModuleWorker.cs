using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Modules.Conditions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.While;

public sealed class WhileModuleWorker(IWorkerContext<WhileModule> context) : ModuleWorker<WhileModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        string conditionModuleId = Module.Condition.Module.ModuleId;
        ModuleArtifacts childArtifacts = ModuleArtifacts.Default with
        {
            Namespace = runtime.Environment.Namespace,
            DecompositionStrategy = DecompositionStrategy.LeavesOnly,
            Environment = ArtifactModuleEnvironment.Default with { Scope = EnvironmentScope.Parent } // need artifacts to be accessible to us
        };
        // loop exits via return statements: either when the condition is no longer met (Success),
        // or when the condition or body module fails (propagated status)
        while (true)
        {
            // don't need a customizable environment for the while condition
            IRuntimeEnvironment environment = runtime.PrepareEnvironment(ModuleEnvironment.Default);
            // force condition to write its artifacts to a known location in the parent environment so we can read it after execution
            // @<child_module_id>.artifacts via @ override of child property
            string artifactsOverride = environment.SyntaxFactory.Path(environment.NamespaceOf(Module.Condition)).Property(Module.Artifacts).Override();
            environment.SetVariable(artifactsOverride, childArtifacts);
            Logger.LogWhileConditionEvaluating(conditionModuleId);
            IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Condition.Module, environment, cancellationToken);
            if (result.Status is ModuleExitStatus.Canceled)
            {
                return runtime.Exit(WithStatus(ModuleExitStatus.Canceled));
            }
            if (result.Status is not ModuleExitStatus.Success)
            {
                // Skipped is not a valid result for condition modules; treat all remaining non-success statuses as failure
                Logger.LogWhileConditionFailed(conditionModuleId, result.Status.ToString());
                return runtime.Exit(WithStatus(ModuleExitStatus.Failed));
            }
            // ${@}.result, via ${@} self reference
            string resultAccessExpression = environment.SyntaxFactory.Self().Ref().Member(nameof(ConditionalResult.Result));
            string resultVariable = runtime.Environment.Interpolate(resultAccessExpression);
            if (!result.Artifacts.TryResolveVariable(resultVariable, out bool condition))
            {
                // this is not a valid result from the condition module
                Logger.LogWhileConditionResultUnreadable(conditionModuleId);
                return runtime.Exit(WithStatus(ModuleExitStatus.Failed));
            }
            bool shouldContinue = condition != Module.InvertCondition;
            if (!shouldContinue)
            {
                Logger.LogWhileLoopExiting(condition);
                return runtime.Exit(WithStatus(ModuleExitStatus.Success));
            }
            Logger.LogWhileBodyExecuting(condition);
            IModuleExecutionResult bodyResult = await runtime.ExecuteAsync(Module.Body, cancellationToken);
            if (bodyResult.Status is not ModuleExitStatus.Success)
            {
                return runtime.Exit(WithStatus(bodyResult.Status));
            }
        }
    }
}
