using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Runtime;

public sealed class DefaultModuleRuntime(DefaultEnvironment defaultEnvironment) : IModuleRuntime
{
    private readonly Dictionary<string, IEnvironment> _environments = new()
    {
        { defaultEnvironment.Name, defaultEnvironment }
    };

    public IEnvironment DefaultEnvironment { get; } = defaultEnvironment;

    public bool TryGetEnvironment(string name, [NotNullWhen(true)] out IEnvironment? environment) => 
        _environments.TryGetValue(name, out environment);

    public bool TryAddEnvironment(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (_environments.ContainsKey(environment.Name))
        {
            return false;
        }
        _environments.Add(environment.Name, environment);
        return true;
    }

    public bool TryRemoveEnvironment(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return _environments.Remove(environment.Name);
    }
}