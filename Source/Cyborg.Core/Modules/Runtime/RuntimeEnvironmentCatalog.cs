using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class RuntimeEnvironmentCatalog
{
    private readonly Dictionary<string, IRuntimeEnvironment> _environments = [];

    public bool TryGet(string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _environments.TryGetValue(name, out environment);
    }

    public bool TryAdd(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.IsTransient || _environments.ContainsKey(environment.Name))
        {
            return false;
        }
        _environments.Add(environment.Name, environment);
        return true;
    }
}
