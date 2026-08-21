using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Core.Text;

namespace Cyborg.Core.Modules.Runtime.Environments;

public interface IVariableResolverScope : IEnumerable<KeyValuePair<string, object?>>
{
    VariableSyntaxBuilder SyntaxFactory { get; }

    /// <summary>
    /// Fully interpolates the template against this scope and finalizes one layer of escaped interpolation literals.
    /// Tags from interpolated values are unioned onto the result.
    /// </summary>
    TaggedString Interpolate(string template);

    /// <summary>
    /// Fully interpolates the template against this scope and finalizes one layer of escaped interpolation literals.
    /// Tags from the template and from interpolated values are unioned onto the result.
    /// </summary>
    TaggedString Interpolate(TaggedString template);

    /// <summary>
    /// Interpolates a nullable tagged template. A null template yields an empty untagged string.
    /// </summary>
    TaggedString Interpolate(TaggedString? template);

    /// <summary>
    /// Fully resolves a stored variable at this scope's entry point, including finalizing one layer of escaped interpolation literals when the result is a string or <see cref="TaggedString"/>.
    /// Prefer <see cref="TaggedString"/> retrieval so tags are preserved. Retrieving a tagged value as <see cref="string"/> discards tags.
    /// </summary>
    bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value);
}
