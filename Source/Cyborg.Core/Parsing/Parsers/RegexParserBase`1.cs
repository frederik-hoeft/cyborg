using Cyborg.Core.Parsing.SyntaxNodes;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Parsing.Parsers;

public abstract class RegexParserBase<TSelf>(string? name) : ParserBase(name) where TSelf : RegexParserBase<TSelf>, IRegexOwner
{
    protected abstract bool TryCreateSyntaxNode([NotNull] Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode);

    public override bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        if (TSelf.ParserRegex.IsMatch(input)                                         // zero-alloc pre-check
            && TSelf.ParserRegex.Match(input.ToString()) is { Success: true } match  // should never fail
            && TryCreateSyntaxNode(match, out syntaxNode))
        {
            charsConsumed = match.Length;
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}

