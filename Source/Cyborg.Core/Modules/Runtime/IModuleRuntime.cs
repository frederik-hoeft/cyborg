using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime;

public interface IModuleRuntime
{
    IEnvironment DefaultEnvironment { get; }

    bool TryGetEnvironment(string name, [NotNullWhen(true)] out IEnvironment? environment);

    bool TryAddEnvironment(IEnvironment environment);

    bool TryRemoveEnvironment(IEnvironment environment);
}
