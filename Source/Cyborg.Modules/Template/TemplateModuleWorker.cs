using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Template;

public sealed class TemplateModuleWorker(IWorkerContext<TemplateModule> context, IModuleConfigurationLoader configurationLoader) : ModuleWorker<TemplateModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        foreach (DynamicKeyValuePair entry in Module.Arguments)
        {
            string key;
            if (entry.Key.StartsWith('@'))
            {
                key = runtime.Environment.SyntaxFactory.Path(Module.Namespace.AsSpan(), entry.Key.AsSpan()[1..]).Override();
            }
            else
            {
                key = runtime.Environment.SyntaxFactory.Path(Module.Namespace, entry.Key);
            }
            runtime.Environment.SetVariable(key, entry.Value);
        }
        ModuleContext moduleContext = await configurationLoader.LoadModuleAsync(Module.Path, cancellationToken);
        IModuleExecutionResult executionResult = await runtime.ExecuteAsync(moduleContext, cancellationToken);
        return runtime.Exit(WithStatus(executionResult.Status));
    }
}