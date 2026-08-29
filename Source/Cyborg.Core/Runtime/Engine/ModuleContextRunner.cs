using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Engine;

internal sealed class ModuleContextRunner(VariableSyntaxBuilder syntaxFactory, IRuntimeEnvironmentFactory environmentFactory, ILoggerFactory loggerFactory) : IModuleContextRunner
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.runtime");

    public async Task<IModuleExecutionResult> ExecuteAsync(IModuleExecutionRuntime runtime, ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(moduleContext);
        ArgumentNullException.ThrowIfNull(environment);

        ResolveRequiredArguments(moduleContext, environment);
        if (moduleContext.Configuration is { } configuration)
        {
            _logger.LogConfigurationModuleRunning(configuration.ModuleId, moduleContext.Module.ModuleId);
            IModuleExecutionResult result = await runtime.ExecuteAsync(configuration, environment, cancellationToken);
            if (result.Status is ModuleExitStatus.Failed or ModuleExitStatus.Canceled)
            {
                _logger.LogModuleConfigurationFailed(configuration.ModuleId, result.Status.ToString(), moduleContext.Module.ModuleId, environment.Name);
                return new ModuleExecutionResult(moduleContext.Module.Definition, ModuleExitStatus.Failed, environmentFactory.CreateEnvironmentLike(environment.Namespace));
            }
        }
        return await runtime.ExecuteModuleReferenceInCurrentScopeAsync(moduleContext.Module, environment, cancellationToken);
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
        _logger.LogTemplateArgumentsResolving(arguments.Count, moduleContext.Module.ModuleId, argumentNamespace);
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
            _logger.LogTemplateArgumentResolutionFailed(moduleContext.Module.ModuleId, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        foreach ((string argument, object value) in resolvedArguments)
        {
            environment.SetVariable(argument, value);
        }
    }
}
