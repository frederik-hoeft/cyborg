using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Visitors;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Runtime.Engine.Expressions.Parsers;

/// <summary>Recognizes escaped interpolation, indirection, and capture tokens without consuming escape layers.</summary>
internal sealed partial class EscapedPartParser(string? name = null) : RegexParserBase<EscapedPartParser, ITextSyntaxVisitor>(name), IRegexOwner, IParser<EscapedPartParser>
{
    public static EscapedPartParser Instance { get; } = new();

    [GeneratedRegex(@"\G(?<operator>[$&*])\{(?<hashes>#+)(?<body>[^}]*)\}")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string newName) => new EscapedPartParser(newName);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        char operatorSymbol = match.Groups["operator"].ValueSpan[0];
        int escapeDepth = match.Groups["hashes"].Length;
        string body = match.Groups["body"].Value;
        syntaxNode = new TextPartNode(new EscapedPartSyntax(operatorSymbol, escapeDepth, body));
        return true;
    }
}
