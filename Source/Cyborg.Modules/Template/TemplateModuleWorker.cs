using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Extensions;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Template;

public sealed class TemplateModuleWorker(IWorkerContext<TemplateModule> context, IModuleConfigurationLoader configurationLoader) : ModuleWorker<TemplateModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        Logger.LogTemplateLoading(Module.Path);
        ModuleContext moduleContext = await configurationLoader.LoadModuleAsync(Module.Path, cancellationToken);
        Logger.LogTemplateLoaded(moduleContext.Module.Module.ModuleId, Module.Path);
        IRuntimeEnvironment environment = runtime.PrepareEnvironment(moduleContext);
        List<string> templateErrors = [];
        foreach (DynamicKeyValuePair entry in Module.Arguments)
        {
            if (!environment.SyntaxFactory.IsValidIdentifier(entry.Key))
            {
                templateErrors.Add($"Argument '{entry.Key}' in module '{Module.ToDisplayString()}' is not a valid identifier.");
                continue;
            }
            PathSyntax key = runtime.Environment.SyntaxFactory.Path(Module.Namespace, entry.Key);
            runtime.Environment.SetVariable(key, entry.Value);
        }
        foreach (DynamicKeyValuePair entry in Module.Overrides)
        {
            ReadOnlySpan<char> key = entry.Key.AsSpan();
            if (!key.StartsWith('@'))
            {
                templateErrors.Add($"Override '{key}' in module '{Module.ToDisplayString()}' must start with '@'.");
                continue;
            }
            ReadOnlySpan<char> identifier = key[1..];
            if (!environment.SyntaxFactory.IsValidIdentifier(identifier))
            {
                templateErrors.Add($"Override identifier '{identifier}' in module '{Module.ToDisplayString()}' is not a valid identifier.");
                continue;
            }
            environment.SetVariable(key, entry.Value);
        }
        if (templateErrors.Count > 0)
        {
            string errorSummary = $"{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", templateErrors)}";
            Logger.LogTemplateConfigurationError(Module.Path, errorSummary);
            throw new InvalidOperationException($"Template module '{Module.ToDisplayString()}' has invalid arguments or overrides:{errorSummary}");
        }
        Logger.LogTemplateArgumentsApplied(Module.Arguments.Count, Module.Overrides.Count, Module.Path);
        IModuleExecutionResult executionResult = await runtime.ExecuteAsync(moduleContext, environment, cancellationToken);
        return runtime.Exit(WithStatus(executionResult.Status));
    }
}
