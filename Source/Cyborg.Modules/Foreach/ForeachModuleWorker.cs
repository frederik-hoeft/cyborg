using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Foreach;

public sealed class ForeachModuleWorker(IWorkerContext<ForeachModule> context) : ModuleWorker<ForeachModule>(context)
{
    protected async override Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        if (!runtime.Environment.TryResolveVariable(Module.Collection, out IEnumerable<object>? collection))
        {
            throw new InvalidOperationException($"Collection variable '{Module.Collection}' not found in the current environment.");
        }
        IRuntimeEnvironment loopEnvironment = runtime.PrepareEnvironment(Module.Body);
        bool result = true;
        foreach (object item in collection)
        {
            DecomposeItem(loopEnvironment, Module.ItemVariable, item);
            bool success = await runtime.ExecuteAsync(Module.Body, loopEnvironment, cancellationToken).ConfigureAwait(false);
            if (!success && !Module.ContinueOnError)
            {
                return runtime.Failure(Module);
            }
            result &= success;
        }
        return runtime.Success(Module);
    }

    private static void DecomposeItem(IRuntimeEnvironment environment, string prefix, object item)
    {
        environment.SetVariable(prefix, item);
        if (item is IDecomposable decomposable)
        {
            foreach ((string key, object? value) in decomposable.Decompose())
            {
                string childPrefix = $"{prefix}.{key}";
                DecomposeItem(environment, childPrefix, value!);
            }
        }
    }
}
