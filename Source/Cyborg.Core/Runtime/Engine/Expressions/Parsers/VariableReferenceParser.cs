using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Runtime.Engine.Expressions.Parsers;

/// <summary>Parses variable identifiers and contextual or entry-point-relative self references.</summary>
internal sealed partial class VariableReferenceParser(string? name = null) : RegexParserBase<VariableReferenceParser>(name), IRegexOwner, IParser<VariableReferenceParser>
{
    public static VariableReferenceParser Instance { get; } = new();

    // The optional entry-point prefix backtracks for solitary '@', which denotes current-scope self.
    [GeneratedRegex(@"\G(?<entry>@)?(?:(?<self>@)|(?<identifier>[A-Za-z_-][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_-]+)*))")]
    public static partial Regex ParserRegex { get; }

    public override IParser NamedCopy(string newName) => new VariableReferenceParser(newName);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string? identifier = match.Groups["identifier"].Success ? match.Groups["identifier"].Value : null;
        ReferenceAnchor anchor = match.Groups["entry"].Success ? ReferenceAnchor.EntryPoint : ReferenceAnchor.CurrentScope;
        syntaxNode = new VariableReferenceNode(new VariableReferenceSyntax(identifier, anchor));
        return true;
    }
}
