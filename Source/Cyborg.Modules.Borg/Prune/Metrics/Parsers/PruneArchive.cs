using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Modules.Borg.Prune.Metrics.Model;
using Cyborg.Modules.Borg.Prune.Metrics.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Modules.Borg.Prune.Metrics;

public sealed partial class PruneArchive(string? name = null) : RegexParserBase<BorgPruneVisitor, PruneArchive>(name), IParser<PruneArchive>, IRegexOwner
{
    [GeneratedRegex(@"^[Pp]runing archive \((?<prune_index>\d+)/(?<prune_total>\d+)\):")]
    public static partial Regex ParserRegex { get; }

    public static PruneArchive Instance { get; } = new();

    public override IParser NamedCopy(string name) => new PruneArchive(name);

    protected override bool TryCreateSyntaxNode([NotNull] Match match, [NotNullWhen(true)] out SyntaxNode<BorgPruneVisitor>? syntaxNode)
    {
        string pruneIndexString = match.Groups["prune_index"].Value;
        string pruneTotalString = match.Groups["prune_total"].Value;
        if (!int.TryParse(pruneIndexString, out int pruneIndex) || !int.TryParse(pruneTotalString, out int pruneTotal))
        {
            syntaxNode = null;
            return false;
        }
        BorgPrunePruneAction result = new(pruneIndex, pruneTotal);
        syntaxNode = new PruneArchiveSyntaxNode(Name, result);
        return true;
    }
}