using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Modules.Borg.Create.InputValidation;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Modules.Borg.Create;

internal sealed partial class Number(int min = int.MinValue, int max = int.MaxValue, string? name = null) : RegexParserBase<Number>(name), IRegexOwner
{
    [GeneratedRegex(@"\G\[\s*(?<number>[1-9][0-9]*)\]")]
    public static partial Regex ParserRegex { get; }

    public int Min => min;

    public int Max => max;

    public override IParser NamedCopy(string name) => new Number(Min, Max, name);

    protected override bool TryCreateSyntaxNode(Match match, [NotNullWhen(true)] out ISyntaxNode? syntaxNode)
    {
        string rowNumberText = match.Groups["number"].Value;
        if (int.TryParse(rowNumberText, out int rowNumber) && rowNumber >= Min && rowNumber <= Max)
        {
            syntaxNode = new NumberSyntaxNode(Name, rowNumber);
            return true;
        }
        syntaxNode = null;
        return false;
    }
}