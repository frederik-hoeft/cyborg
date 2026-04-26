using Cyborg.Core.Parsing.SyntaxNodes;

namespace Cyborg.Core.Parsing.Parsers;

public sealed class End(string? name = null) : ParserBase(name), IParser<End>
{
    public static End Instance { get; } = new();

    public override IParser NamedCopy(string name) => new End(name);

    public override bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        charsConsumed = 0;
        if (input.Length != 0)
        {
            syntaxNode = null;
            return false;
        }
        syntaxNode = new EndSyntaxNode(Name);
        return true;
    }
}
