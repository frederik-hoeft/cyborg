using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Modules.Borg.Prune.Metrics.Model;
using Cyborg.Modules.Borg.Prune.Metrics.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Modules.Borg.Prune.Metrics;

public sealed partial class KeepArchive(string? name = null) : RegexParserBase<BorgPruneVisitor, KeepArchive>(name), IParser<KeepArchive>, IRegexOwner
{
    [GeneratedRegex(@"^[Kk]eeping archive \(rule: (?<rule>.+?) #(?<rule_index>\d+)\):")]
    public static partial Regex ParserRegex { get; }

    public static KeepArchive Instance { get; } = new();

    public override IParser NamedCopy(string name) => new KeepArchive(name);

    protected override bool TryCreateSyntaxNode([NotNull] Match match, [NotNullWhen(true)] out SyntaxNode<BorgPruneVisitor>? syntaxNode)
    {
        string ruleName = match.Groups["rule"].Value;
        string ruleIndexString = match.Groups["rule_index"].Value;
        if (!int.TryParse(ruleIndexString, out int ruleIndex))
        {
            syntaxNode = null;
            return false;
        }
        BorgPruneKeepAction result = new(ruleName, ruleIndex);
        syntaxNode = new KeepArchiveSyntaxNode(Name, result);
        return true;
    }
}