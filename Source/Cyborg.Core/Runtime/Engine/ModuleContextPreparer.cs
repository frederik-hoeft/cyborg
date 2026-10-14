using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Text;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Resolves context requirements and runs the optional configuration child invocation.
/// The caller owns dispatch of the primary reference within the enclosing invocation.
/// </summary>
internal sealed class ModuleContextPreparer(VariableSyntaxBuilder syntaxFactory, IRuntimeEnvironmentFactory environmentFactory, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("cyborg.core.runtime");

    /// <summary>
    /// Returns a failure result if the configuration child fails, or <see langword="null"/>
    /// when the primary reference can be dispatched in the existing invocation.
    /// </summary>
    public async Task<IModuleExecutionResult?> PrepareAsync(IModuleRuntime runtime, ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken)
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
        return null;
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
            object storedValue = value is string or TaggedString ? ExpressionShield.ShieldText(value) : value;
            environment.SetVariable(argument, storedValue);
        }
    }
}
