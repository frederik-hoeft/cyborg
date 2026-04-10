using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Conditions.DirectoryExists;

public sealed class DirectoryExistsModuleWorker(IWorkerContext<DirectoryExistsModule> context) : ModuleWorker<DirectoryExistsModule>(context)
{
    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        bool exists = Directory.Exists(Module.Path);
        return Task.FromResult(runtime.Exit(Success(new ConditionalResult(exists))));
    }
}
