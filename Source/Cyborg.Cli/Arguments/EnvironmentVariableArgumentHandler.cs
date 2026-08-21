using Cyborg.Cli.Logging;
using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;

namespace Cyborg.Cli.Arguments;

internal sealed class EnvironmentVariableArgumentHandler
(
    IDynamicValueProviderRegistry providerRegistry,
    IJsonLoaderContext jsonLoaderContext,
    ILoggerFactory loggerFactory
) : IEnvironmentVariableArgumentHandler
{
    public bool TryProcessArgument(string[]? environmentVariables, IEnvironmentLike environment)
    {
        if (environmentVariables is not [_, ..])
        {
            return true;
        }

        ILogger logger = loggerFactory.CreateLogger("cyborg.cli.argument-handling");
        foreach (string definition in environmentVariables)
        {
            if (!DynamicArgumentDefinitionParser.TryParse(definition, out DynamicArgumentDefinition parsed, out string? parseError))
            {
                logger.LogInvalidEnvironmentVariable(definition, parseError);
                return false;
            }
            if (!environment.SyntaxFactory.IsValidIdentifier(parsed.Key))
            {
                logger.LogInvalidEnvironmentVariable(definition, $"'{parsed.Key}' is not a valid variable identifier.");
                return false;
            }

            object? value;
            if (parsed.TypeName is null)
            {
                // assume value is string if no type specified, to avoid unnecessary JSON parsing for common case of string values
                value = parsed.Value;
            }
            else
            {
                if (!providerRegistry.TryGetProvider(parsed.TypeName, out IDynamicValueProvider? provider))
                {
                    logger.LogInvalidEnvironmentVariable(definition, $"Unknown dynamic value type '{parsed.TypeName}'.");
                    return false;
                }
                if (!DynamicArgumentValueParser.TryParse(provider, jsonLoaderContext, parsed.Value, out value, out string? dynamicValueError))
                {
                    logger.LogInvalidEnvironmentVariable(definition, $"Value is not valid for dynamic value type '{parsed.TypeName}': {dynamicValueError}");
                    return false;
                }
            }

            environment.SetVariable(parsed.Key, value);
        }
        return true;
    }
}
