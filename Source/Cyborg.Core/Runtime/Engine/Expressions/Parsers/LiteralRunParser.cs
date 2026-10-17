using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Visitors;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Runtime.Engine.Expressions.Parsers;

/// <summary>Consumes literal text up to the next recognized token; the token grammar defines those boundaries.</summary>
internal sealed class LiteralRunParser(IParser tokenParser, string? name = null) : ParserBase<ITextSyntaxVisitor>
{
    public override string? Name => name;

    public override IParser NamedCopy(string newName) => new LiteralRunParser(tokenParser, newName);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offset, (uint)input.Length);
        int end = offset;
        while (end < input.Length && !tokenParser.TryParse(input, end, out _, out _))
        {
            end++;
        }
        charsConsumed = end - offset;
        syntaxNode = charsConsumed > 0 ? new TextPartNode(new LiteralPartSyntax(input[offset..end])) : null;
        return syntaxNode is not null;
    }
}
