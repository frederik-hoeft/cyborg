using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Dynamic;

public sealed class DynamicModuleWorker(IWorkerContext<DynamicModule> context) : ModuleWorker<DynamicModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        IRuntimeEnvironment environment = runtime.PrepareEnvironment(Module.Target);
        if (Module.Tags is { Count: > 0 } tags)
        {
            foreach (string tag in tags)
            {
                if (!runtime.Environment.SyntaxFactory.IsValidIdentifier(tag))
                {
                    ThrowInvalidIdentifier(tag);
                }
                environment.OverrideResolutionTags.Add(tag);
            }
        }
        IModuleExecutionResult result = await runtime.ExecuteAsync(Module.Target, cancellationToken);
        return runtime.Exit(WithStatus(result.Status));
    }

    [DoesNotReturn]
    private static void ThrowInvalidIdentifier(string identifier) => throw new FormatException($"Override identifier '{identifier}' does not match the required format.");
}