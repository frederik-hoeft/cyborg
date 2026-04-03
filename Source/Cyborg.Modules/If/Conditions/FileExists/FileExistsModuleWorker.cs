using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.If.Conditions.FileExists;

public sealed class FileExistsModuleWorker(IWorkerContext<FileExistsModule> context) : ModuleWorker<FileExistsModule>(context)
{
    protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (!runtime.Environment.TryResolveVariable(Module.Path, out string? path))
        {
            // undefined variable or type mismatch
            return Task.FromResult(runtime.Exit(Failed()));
        }
        bool exists = File.Exists(path);
        return Task.FromResult(runtime.Exit(Success(new ConditionalResult(exists))));
    }
}
