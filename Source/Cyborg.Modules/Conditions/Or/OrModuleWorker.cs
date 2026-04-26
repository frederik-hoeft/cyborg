using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Conditions.Or;

public sealed class OrModuleWorker(IWorkerContext<OrModule> context) : ConditionalCombinatorModuleWorkerBase<OrModule>(context)
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
            if (result)
            {
                return runtime.Exit(Success(new ConditionalResult(true)));
            }
            ++index;
        }
        return runtime.Exit(Success(new ConditionalResult(false)));
    }
}
