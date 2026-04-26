using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Modules.Borg.Prune.Metrics.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Modules.Borg.Prune.Metrics;

public sealed partial class ArchiveId(string? name = null) : RegexParserBase<BorgPruneVisitor, ArchiveId>(name), IParser<ArchiveId>, IRegexOwner
{
    [GeneratedRegex(@"^\[(?<archive_id>[0-9a-f]{64}|[0-9A-F]{64})\]")]
    public static partial Regex ParserRegex { get; }

    public static ArchiveId Instance { get; } = new();

    public override IParser NamedCopy(string name) => new ArchiveId(name);

    protected override bool TryCreateSyntaxNode([NotNull] Match match, [NotNullWhen(true)] out SyntaxNode<BorgPruneVisitor>? syntaxNode)
    {
        string archiveId = match.Groups["archive_id"].Value;
        syntaxNode = new ArchiveIdSyntaxNode(Name, archiveId);
        return true;
    }
}
