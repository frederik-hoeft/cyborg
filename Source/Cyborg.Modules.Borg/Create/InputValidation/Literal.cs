using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Create.InputValidation;

internal sealed class Literal(string value, string? name = null) : ParserBase(name)
{
    public override IParser NamedCopy(string name) => new Literal(value, name);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ReadOnlySpan<char> inputSpan = input.AsSpan(offset);
        if (inputSpan.StartsWith(value, StringComparison.Ordinal))
        {
            charsConsumed = value.Length;
            syntaxNode = new LiteralSyntaxNode(Name, value);
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}