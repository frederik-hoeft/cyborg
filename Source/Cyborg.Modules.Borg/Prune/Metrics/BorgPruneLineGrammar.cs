using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Modules.Borg.Prune.Metrics.Model;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Prune.Metrics;

internal static class BorgPruneLineGrammar
{
    // Pruning archive (1/1):                       teamspeak-2026-02-01T04:50:18        Sun, 2026-02-01 04:50:19 [c470fb03260ea70a4a81cb9cd25c9a1570b7ec784d464c878d926317005bc1a2]
    // Keeping archive (rule: monthly #1):          teamspeak-2026-01-28T20:28:09        Wed, 2026-01-28 20:28:10 [6ab84efc2229d5a98519be6b78a1322e5fda2ee4529874a7a8b12a369b24c70e]
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "That would kill readability for CRTP-style grammar definitions.")]
    private static readonly IParser s_grammar = Sequence<
        Alternative<
            PruneArchive,
            KeepArchive>,
        Whitespace,
        ArchiveName,
        Whitespace,
        ArchiveTimestamp,
        Whitespace,
        ArchiveId,
        End>.Instance;

    public static bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out BorgPruneLineModel? model)
    {
        if (!s_grammar.TryParse(input, out ISyntaxNode? syntaxNode, out _))
        {
            model = null;
            return false;
        }
        BorgPruneLineModelBuilder modelBuilder = new();
        BorgPruneVisitor visitor = new(modelBuilder);
        syntaxNode.Accept(visitor);
        return modelBuilder.TryBuild(out model);
    }
}
