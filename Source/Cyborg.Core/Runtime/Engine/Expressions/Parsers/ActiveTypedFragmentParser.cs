using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Visitors;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Runtime.Engine.Expressions.Parsers;

/// <summary>Identifies active typed fragments so the enclosing text grammar rejects embedded or malformed typed expressions.</summary>
internal sealed partial class ActiveTypedFragmentParser(string? name = null) : RegexParserBase<ActiveTypedFragmentParser, ITextSyntaxVisitor>(name), IRegexOwner, IParser<ActiveTypedFragmentParser>
{
    public static ActiveTypedFragmentParser Instance { get; } = new();

    [GeneratedRegex(@"\G[&*]\{(?!#)[^}]*\}")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string newName) => new ActiveTypedFragmentParser(newName);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        syntaxNode = new RejectedTypedFragmentNode(match.Value);
        return true;
    }
}
