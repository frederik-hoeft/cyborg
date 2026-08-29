using Cyborg.Core.Runtime.Engine.Environments;

namespace Cyborg.Cli.Arguments;

internal interface IEnvironmentVariableArgumentHandler
{
    bool TryProcessArgument(string[]? environmentVariables, IEnvironmentLike environment);
}
