using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Model;
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
            (bool result, ModuleExitStatus status) = await ExecuteConditionAsync(runtime, condition, childNamespace, cancellationToken);
            if (status is not ModuleExitStatus.Success)
            {
                return runtime.Exit(WithStatus(status));
            }
            if (!result)
            {
                return runtime.Exit(Success(new ConditionalResult(false)));
            }
            ++index;
        }
        return runtime.Exit(Success(new ConditionalResult(true)));
    }
}
