using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.If.Conditions.DirectoryExists;

public sealed class DirectoryExistsModuleWorker(IWorkerContext<DirectoryExistsModule> context) : ModuleWorker<DirectoryExistsModule>(context)
{
    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (!runtime.Environment.TryResolveVariable(Module.Path, out string? path))
        {
            // undefined variable or type mismatch
            return Task.FromResult(runtime.Exit(Failed()));
        }
        bool exists = Directory.Exists(path);
        return Task.FromResult(runtime.Exit(Success(new ConditionalResult(exists))));
    }
}
