using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Runtime.Engine.Environments.Artifacts;

namespace Cyborg.Core.Runtime.Engine.Environments;

public interface IEnvironmentLike : IVariableResolverScope
{
    string Namespace { get; }

    void Publish(string root, IDecomposable decomposable, DecompositionStrategy strategy, bool publishNullValues);

    void SetVariable<T>(string name, T value);

    bool TryRemoveVariable(string name);
}
