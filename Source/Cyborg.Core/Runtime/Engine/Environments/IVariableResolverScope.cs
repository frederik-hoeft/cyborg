using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Text;

namespace Cyborg.Core.Runtime.Engine.Environments;

public interface IVariableResolverScope : IEnumerable<KeyValuePair<string, object?>>
{
    VariableSyntaxBuilder SyntaxFactory { get; }

    /// <summary>
    /// Fully interpolates <c>${...}</c> in the template and finalizes one layer of escaped literals.
    /// Tags from interpolated values are unioned onto the result.
    /// <c>&amp;{...}</c> and <c>*{...}</c> are syntax errors in this textual operation.
    /// </summary>
    TaggedString Interpolate(string template);

    /// <summary>
    /// Fully interpolates <c>${...}</c> in the template and finalizes one layer of escaped literals.
    /// Tags from the template and from interpolated values are unioned onto the result.
    /// <c>&amp;{...}</c> and <c>*{...}</c> are syntax errors in this textual operation.
    /// </summary>
    TaggedString Interpolate(TaggedString template);

    /// <summary>
    /// Interpolates a nullable tagged template. A null template yields an empty untagged string.
    /// </summary>
    TaggedString Interpolate(TaggedString? template);

    /// <summary>
    /// Fully resolves a stored variable at this scope's entry point.
    /// Text results finalize one escape layer unless the value is an eager-capture snapshot, which is returned unchanged.
    /// Prefer <see cref="TaggedString"/> retrieval so tags are preserved. Retrieving a tagged value as <see cref="string"/> discards tags.
    /// </summary>
    bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value);
}
