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
        foreach (string env in environmentVariables)
        {
            if (!TryParseSplit(env, '=', out ReadOnlySpan<char> keyPart, out ReadOnlySpan<char> value, enforceSingleDelimiter: false))
            {
                logger.LogInvalidEnvironmentVariable();
                return false;
            }
            object valueObj;
            if (TryParseSplit(keyPart, ':', out ReadOnlySpan<char> key, out ReadOnlySpan<char> dataType, enforceSingleDelimiter: true))
            {
                string typeName = dataType.ToString();
                if (!providerRegistry.TryGetProvider(typeName, out IDynamicValueProvider? provider))
                {
                    logger.LogUnknownEnvironmentVariableType(typeName);
                    return false;
                }
                if (!DynamicArgumentValueParser.TryParse(provider, jsonLoaderContext, value.ToString(), out object? dynamicValue, out _))
                {
                    logger.LogInvalidEnvironmentVariable();
                    return false;
                }
                valueObj = dynamicValue;
            }
            else
            {
                key = keyPart;
                // assume value is string if no type specified, to avoid unnecessary JSON parsing for common case of string values
                valueObj = value.ToString();
            }
            if (!environment.SyntaxFactory.IsValidIdentifier(key))
            {
                logger.LogInvalidEnvironmentVariable();
                return false;
            }
            environment.SetVariable(key.ToString(), valueObj);
        }
        return true;
    }

    private static bool TryParseSplit(ReadOnlySpan<char> input, char delimiter, out ReadOnlySpan<char> left, out ReadOnlySpan<char> right, bool enforceSingleDelimiter)
    {
        int splitIndex = input.IndexOf(delimiter);
        int splitCheckIndex = input.LastIndexOf(delimiter);
        if (splitIndex <= 0 || enforceSingleDelimiter && splitIndex != splitCheckIndex)
        {
            left = right = default;
            return false;
        }
        left = input[..splitIndex];
        right = input[(splitIndex + 1)..];
        return true;
    }
}
