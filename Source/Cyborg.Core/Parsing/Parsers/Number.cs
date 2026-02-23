using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Parsing.Parsers;

public class Number : RegexParserBase<Number>, IParser<Number>, IRegexOwner
{
    protected Number(string? name) : base(name)
    {
    }

    public static Regex ParserRegex => throw new NotImplementedException();

    public static Number Instance => throw new NotImplementedException();

    public override IParser NamedCopy(string name) => throw new NotImplementedException();

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode) => throw new NotImplementedException();
}
