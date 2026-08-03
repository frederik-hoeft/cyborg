using Cyborg.Core.Modules.Runtime.Environments.Syntax;

namespace Cyborg.Core.Modules.Runtime.Environments;

public interface IVariableResolverScope : IEnumerable<KeyValuePair<string, object?>>
{
    VariableSyntaxBuilder SyntaxFactory { get; }

    /// <summary>
    /// Fully interpolates the template against this scope and finalizes one layer of escaped interpolation literals.
    /// </summary>
    string Interpolate(string template);

    /// <summary>
    /// Performs the generated final interpolation phase: resolves ordinary expressions, then removes exactly one leading hash from each <c>${#...}</c> literal without rescanning the revealed expression.
    /// </summary>
    string InterpolateFinal(string template);

    /// <summary>
    /// Fully resolves a stored variable at this scope's entry point, including finalizing one layer of escaped interpolation literals when the result is a string.
    /// </summary>
    bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value);
}
