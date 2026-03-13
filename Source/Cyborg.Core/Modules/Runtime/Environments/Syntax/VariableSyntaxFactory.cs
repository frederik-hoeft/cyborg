using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keeping instance members for consistency and potential future state needs.")]
public sealed class VariableSyntaxFactory(IRuntimeEnvironment environment, JsonNamingPolicy namingPolicy)
{
    public OverrideSyntaxNode Override(string variable) => Override(Variable(variable));

    public OverrideSyntaxNode Override(VariableSyntaxNode variable) => new(variable);

    public VariableSyntaxNode Variable(string variable) => new(variable);

    public VariableSyntaxNode MemberVariable(string memberName) => new(namingPolicy.ConvertName(memberName));

    public NamespaceSyntaxNode Namespace(string ns) => new(null!, ns);

    public NamespaceSyntaxNode Namespace(NamespaceSyntaxNode parent, string ns) => new(parent, ns);

    public VariableSyntaxNode Variable(NamespaceSyntaxNode ns, string variable)
    {
        ArgumentNullException.ThrowIfNull(ns);
        return ns.Combine(Variable(variable));
    }

    public NamespaceSyntaxNode Namespace() => new(null!, environment.EffectiveNamespace!);

    public NamespaceSyntaxNode Self() => Namespace(environment.Self);

    public VariableRefSyntaxNode Ref(string variable) => Ref(Variable(variable));

    public VariableRefSyntaxNode Ref(VariableSyntaxNode variable) => new(variable);

    public VariableRefSyntaxNode Ref(NamespaceSyntaxNode ns) => new(ns);

    public VariableSyntaxNode Variable(string ns, string variable) => Namespace(ns).Combine(Variable(variable));
}