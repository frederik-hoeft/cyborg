using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Cyborg.Core.Parsing.SyntaxNodes;

namespace Cyborg.Core.Parsing.Parsers;

public abstract class RegexParserBase<TSelf>(string? name) : ParserBase
    where TSelf : RegexParserBase<TSelf>, IRegexOwner
{
    public override string? Name { get; } = name;

    protected abstract bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)offset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        Match match = TSelf.ParserRegex.Match(input, offset);
        if (match.Success && match.Index == offset && TryCreateSyntaxNode(match, out syntaxNode))
        {
            charsConsumed = match.Length;
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}
