using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;
using System.Collections;

namespace Cyborg.Core.Modules.Runtime.Artifacts;

internal sealed class DefaultModuleArtifacts<TModule>(TModule module) : IModuleArtifactsBuilder, IModuleArtifacts where TModule : ModuleBase, IModule
{
    private const string DEFAULT_ARTIFACT_NAME = "artifacts";
    private readonly Dictionary<string, object?> _artifacts = [];
    private readonly string _defaultNamespace = module switch
    {
        { Artifacts.CustomNamespace: { Length: > 0 } customNamespace } => customNamespace,
         _ when module.Name is { } name && !string.IsNullOrWhiteSpace(name) => name,
         _ => TModule.ModuleId
    };

    IEnumerable<string> IReadOnlyDictionary<string, object?>.Keys => _artifacts.Keys;

    IEnumerable<object?> IReadOnlyDictionary<string, object?>.Values => _artifacts.Values;

    int IReadOnlyCollection<KeyValuePair<string, object?>>.Count => _artifacts.Count;

    object? IReadOnlyDictionary<string, object?>.this[string key] => _artifacts[key];

    public IModuleArtifactsBuilder Expose(string name, object? artifact) => Expose(_defaultNamespace, name, artifact);

    public IModuleArtifactsBuilder Expose(string ns, string name, object? artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ns);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        AddArtifact(CreateArtifactPath(name, ns), artifact);
        return this;
    }

    IModuleArtifactsBuilder IModuleArtifactsBuilder.Expose<T>(T artifact) => Expose(_defaultNamespace, artifact);

    IModuleArtifactsBuilder IModuleArtifactsBuilder.Expose<T>(string ns, T artifact) => Expose(ns, DEFAULT_ARTIFACT_NAME, artifact);

    void IModuleArtifactsBuilder.PublishToEnvironment(IRuntimeEnvironment environment, ModuleExitStatus exitStatus)
    {
        ArgumentNullException.ThrowIfNull(environment);
        foreach ((string key, object? value) in _artifacts)
        {
            if (value is IDecomposable decomposable)
            {
                environment.Publish(module, key, decomposable);
            }
            else
            {
                environment.SetVariable(key, value);
            }
        }
        environment.SetVariable(CreateArtifactPath(module.Artifacts.ExitStatusName), exitStatus);
    }

    private void AddArtifact(string path, object? artifact)
    {
        if (_artifacts.ContainsKey(path))
        {
            throw new InvalidOperationException($"An artifact with the path '{path}' has already been exposed. Artifact paths must be unique within the module context.");
        }
        _artifacts.Add(path, artifact);
    }

    private string CreateArtifactPath(string name, string? ns = null)
    {
        string effectiveNamespace = string.IsNullOrWhiteSpace(ns) ? _defaultNamespace : ns;
        return $"@{effectiveNamespace}.{name}";
    }

    bool IReadOnlyDictionary<string, object?>.ContainsKey(string key) => _artifacts.ContainsKey(key);

    bool IReadOnlyDictionary<string, object?>.TryGetValue(string key, out object? value) => _artifacts.TryGetValue(key, out value);

    IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator() => _artifacts.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _artifacts.GetEnumerator();
}