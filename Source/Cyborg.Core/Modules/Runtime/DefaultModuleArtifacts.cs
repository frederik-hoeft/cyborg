using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class DefaultModuleArtifacts : IModuleArtifacts
{
    private readonly Dictionary<string, object?> _artifacts = [];

    public IModuleArtifacts Expose(string name, object? artifact)
    {
        ArgumentNullException.ThrowIfNull(name);
        _artifacts[name] = artifact;
        return this;
    }

    void IModuleArtifacts.PublishToEnvironment(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        foreach (KeyValuePair<string, object?> kvp in _artifacts)
        {
            environment.SetVariable(kvp.Key, kvp.Value);
        }
    }
}