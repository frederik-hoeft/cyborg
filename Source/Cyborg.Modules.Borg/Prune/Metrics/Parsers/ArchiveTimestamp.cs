using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Modules.Borg.Prune.Metrics.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Cyborg.Modules.Borg.Prune.Metrics;

public sealed partial class ArchiveTimestamp(string? name = null) : RegexParserBase<BorgPruneVisitor, ArchiveTimestamp>(name), IParser<ArchiveTimestamp>, IRegexOwner
{
    // Sun, 2026-02-01 04:50:19
    [GeneratedRegex(@"^(?<date_time>[A-Za-z]{3}, \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})")]
    public static partial Regex ParserRegex { get; }

    public static ArchiveTimestamp Instance { get; } = new();

    public override IParser NamedCopy(string name) => new ArchiveTimestamp(name);

    protected override bool TryCreateSyntaxNode([NotNull] Match match, [NotNullWhen(true)] out SyntaxNode<BorgPruneVisitor>? syntaxNode)
    {
        string dateTimeString = match.Groups["date_time"].Value;
        if (!DateTime.TryParseExact(dateTimeString, "ddd, yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
        {
            syntaxNode = null;
            return false;
        }
        syntaxNode = new ArchiveTimestampSyntaxNode(Name, dateTime);
        return true;
    }
}
