using Cyborg.Core.Modules.Runtime.Environments.Syntax;

namespace Cyborg.Core.Modules.Runtime.Environments;

public interface IVariableResolverScope : IEnumerable<KeyValuePair<string, object?>>
{
    VariableSyntaxBuilder SyntaxFactory { get; }

    string Interpolate(string template);

    bool TryResolveVariable<T>(string name, [NotNullWhen(true)] out T? value);
}
