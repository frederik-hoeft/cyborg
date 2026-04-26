using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Prune.Metrics.SyntaxNodes;

public sealed class ArchiveTimestampSyntaxNode(string? name, DateTime result) : SyntaxNode<BorgPruneVisitor, DateTime>(name, result)
{
    protected override void Accept([NotNull] BorgPruneVisitor visitor) => visitor.Accept(this);
}
