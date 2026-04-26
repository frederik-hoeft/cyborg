using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Cli.Arguments;

internal interface IEnvironmentVariableArgumentHandler
{
    bool TryProcessArgument(string[]? environmentVariables, IEnvironmentLike environment);
}
