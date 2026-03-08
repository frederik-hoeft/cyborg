using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Foreach;

public sealed class ForeachModuleWorker(ForeachModule module) : ModuleWorker<ForeachModule>(module)
{
    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        string collectionVariable = runtime.Environment.Resolve(Module, Module.Collection);
        string itemVariable = runtime.Environment.Resolve(Module, Module.ItemVariable);
        bool continueOnError = runtime.Environment.Resolve(Module, Module.ContinueOnError);
        ModuleContext body = Module.Body;

        if (!runtime.Environment.TryResolveVariable(collectionVariable, out IEnumerable<object>? collection))
        {
            throw new InvalidOperationException($"Collection variable '{collectionVariable}' not found in the current environment.");
        }
        IRuntimeEnvironment loopEnvironment = runtime.PrepareEnvironment(body);
        bool result = true;
        foreach (object item in collection)
        {
            DecomposeItem(loopEnvironment, itemVariable, item);
            bool success = await runtime.ExecuteAsync(body, loopEnvironment, cancellationToken).ConfigureAwait(false);
            if (!success && !continueOnError)
            {
                return false;
            }
            result &= success;
        }
        return result;
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
