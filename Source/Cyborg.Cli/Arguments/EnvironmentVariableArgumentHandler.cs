using Cyborg.Cli.Logging;
using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Configuration.Serialization.Dynamics;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

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
            if (!TryParseSplit(env, '=', out ReadOnlySpan<char> keyPart, out ReadOnlySpan<char> value))
            {
                logger.LogInvalidEnvironmentVariable(env);
                return false;
            }
            object valueObj;
            if (TryParseSplit(keyPart, ':', out ReadOnlySpan<char> key, out ReadOnlySpan<char> dataType))
            {
                string typeName = dataType.ToString();
                if (!providerRegistry.TryGetProvider(typeName, out IDynamicValueProvider? provider))
                {
                    logger.LogUnknownEnvironmentVariableType(env, typeName);
                    return false;
                }
                Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(value.ToString()));
                reader.Read();
                if (!provider.TryCreateValue(ref reader, jsonLoaderContext, out DynamicValue? dynamicValue))
                {
                    logger.LogInvalidEnvironmentVariable(env);
                    return false;
                }
                valueObj = dynamicValue.Value;
            }
            else
            {
                key = keyPart;
                // assume value is string if no type specified, to avoid unnecessary JSON parsing for common case of string values
                valueObj = value.ToString();
            }
            if (!environment.SyntaxFactory.IsValidIdentifier(key))
            {
                logger.LogInvalidEnvironmentVariable(env);
                return false;
            }
            environment.SetVariable(key.ToString(), valueObj);
        }
        return true;
    }

    private static bool TryParseSplit(ReadOnlySpan<char> input, char delimiter, out ReadOnlySpan<char> left, out ReadOnlySpan<char> right)
    {
        int splitIndex = input.IndexOf(delimiter);
        int splitCheckIndex = input.LastIndexOf(delimiter);
        if (splitIndex <= 0 || splitIndex != splitCheckIndex)
        {
            left = right = default;
            return false;
        }
        left = input[..splitIndex];
        right = input[(splitIndex + 1)..];
        return true;
    }
}