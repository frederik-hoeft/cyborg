using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments;

public sealed record GlobalRuntimeEnvironment : RuntimeEnvironment
{
    public new JsonNamingPolicy NamingPolicy => SyntaxFactory.NamingPolicy;

    public GlobalRuntimeEnvironment(JsonNamingPolicy namingPolicy)
        : this(new VariableSyntaxBuilder(namingPolicy))
    {
    }

    internal GlobalRuntimeEnvironment(VariableSyntaxBuilder syntaxFactory)
        : base(Name: "global", IsTransient: false, syntaxFactory, Namespace: string.Empty)
    {
    }
}
