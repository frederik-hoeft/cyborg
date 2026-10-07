using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Runtime.Engine.Environments.Artifacts;

namespace Cyborg.Core.Runtime.Engine.Environments;

public interface IEnvironmentLike : IVariableResolverScope
{
    string Namespace { get; }

    void Publish(string root, IDecomposable decomposable, DecompositionStrategy strategy, bool publishNullValues);

    /// <summary>
    /// Stores <paramref name="value"/>. An exact <c>*{identifier}</c> string is captured immediately.
    /// An exact <c>&amp;{...}</c> string is stored as a lazy typed reference. Embedded unescaped
    /// <c>&amp;{...}</c> or <c>*{...}</c> text is a syntax error.
    /// </summary>
    void SetVariable<T>(string name, T value);

    bool TryRemoveVariable(string name);
}
