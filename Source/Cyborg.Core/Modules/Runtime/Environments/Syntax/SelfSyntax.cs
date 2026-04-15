using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments.Syntax;

public readonly record struct SelfSyntax
{
    private JsonNamingPolicy NamingPolicy { get; }

    internal SelfSyntax(JsonNamingPolicy namingPolicy)
    {
        NamingPolicy = namingPolicy ?? throw new ArgumentNullException(nameof(namingPolicy));
    }

    public RefSyntax Ref() => new(NamingPolicy, ToString());

    public LateRefSyntax LateRef() => new(NamingPolicy, ToString());

    public override string ToString() => "@";

    public static implicit operator string(SelfSyntax self) => self.ToString();
}