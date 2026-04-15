using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Modules.Borg.Prune.Metrics.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cyborg.Modules.Borg.Prune.Metrics;

public sealed partial class ArchiveName(string? name = null) : RegexParserBase<BorgPruneVisitor, ArchiveName>(name), IParser<ArchiveName>, IRegexOwner
{
    [GeneratedRegex(@"^(?<archive_name>\S+)")]
    public static partial Regex ParserRegex { get; }

    public static ArchiveName Instance { get; } = new();

    public override IParser NamedCopy(string name) => new ArchiveName(name);

    protected override bool TryCreateSyntaxNode([NotNull] Match match, [NotNullWhen(true)] out SyntaxNode<BorgPruneVisitor>? syntaxNode)
    {
        string archiveName = match.Groups["archive_name"].Value;
        syntaxNode = new ArchiveNameSyntaxNode(Name, archiveName);
        return true;
    }
}
