using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ModuleContextExecutor(VariableSyntaxBuilder syntaxFactory, ILogger logger)
{
    public async Task<IModuleExecutionResult> ExecuteAsync(
        IModuleRuntime runtime,
        ModuleContext moduleContext,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);

        ResolveRequiredArguments(moduleContext, environment);
        if (moduleContext.Configuration is { } configuration)
        {
            logger.LogConfigurationModuleRunning(configuration.ModuleId, moduleContext.Module.ModuleId);
            IModuleExecutionResult result = await runtime.ExecuteAsync(configuration, environment, cancellationToken);
            if (result.Status is ModuleExitStatus.Failed or ModuleExitStatus.Canceled)
            {
                logger.LogModuleConfigurationFailed(configuration.ModuleId, result.Status.ToString(), moduleContext.Module.ModuleId, environment.Name);
                return new ModuleExecutionResult(moduleContext.Module.Definition, ModuleExitStatus.Failed, environment.CreateArtifactCollection());
            }
        }
        return await runtime.ExecuteAsync(moduleContext.Module, environment, cancellationToken);
    }

    private void ResolveRequiredArguments(ModuleContext moduleContext, IRuntimeEnvironment environment)
    {
        ModuleRequirements? requirements = moduleContext.Requires;
        if (requirements is null || requirements.Arguments.Count == 0)
        {
            return;
        }

        string? argumentNamespaceValue = requirements.ArgumentNamespace;
        IReadOnlyCollection<string> arguments = requirements.Arguments;
        List<string> errors = [];
        List<(string Argument, object Value)> resolvedArguments = [];
        string argumentNamespace = argumentNamespaceValue ?? "(none)";
        logger.LogTemplateArgumentsResolving(arguments.Count, moduleContext.Module.ModuleId, argumentNamespace);
        if (!string.IsNullOrEmpty(argumentNamespaceValue) && !syntaxFactory.IsValidIdentifier(argumentNamespaceValue))
        {
            errors.Add($"Template namespaces must be valid identifiers: '{argumentNamespaceValue}'");
        }
        int i = -1;
        foreach (string argument in arguments)
        {
            ++i;
            if (!syntaxFactory.IsValidIdentifier(argument))
            {
                errors.Add($"Template argument names must be valid identifiers: argv[{i}] = '{argument}'");
                continue;
            }
            PathSyntax path = syntaxFactory.Path(argument);
            PathSyntax argumentPath = syntaxFactory.Path(argumentNamespaceValue).Child(path);
            if (environment.TryResolveVariable(argumentPath, out object? value) || environment.TryResolveVariable(path, out value))
            {
                resolvedArguments.Add((argument, value));
                continue;
            }
            errors.Add($"Unable to resolve template argument: '{argument}'");
        }
        if (errors.Count > 0)
        {
            string errorMessage = $"Module execution failed due to missing required arguments:{System.Environment.NewLine}    {string.Join($"{System.Environment.NewLine}    ", errors)}";
            logger.LogTemplateArgumentResolutionFailed(moduleContext.Module.ModuleId, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        foreach ((string argument, object value) in resolvedArguments)
        {
            environment.SetVariable(argument, value);
        }
    }
}
