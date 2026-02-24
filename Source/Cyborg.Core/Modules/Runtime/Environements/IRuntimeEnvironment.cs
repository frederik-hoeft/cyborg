using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime.Environements;

public interface IRuntimeEnvironment
{
    string Name { get; }

    bool IsTransient { get; }

    bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value);

    void SetVariable<T>(string name, T value);

    bool TryRemoveVariable(string name);
}