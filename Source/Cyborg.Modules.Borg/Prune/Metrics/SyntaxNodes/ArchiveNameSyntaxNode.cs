using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Prune.Metrics.SyntaxNodes;

public sealed class ArchiveNameSyntaxNode(string? name, string result) : SyntaxNodeBase<BorgPruneVisitor, string>(name, result)
{
    protected override void Accept([NotNull] BorgPruneVisitor visitor) => visitor.Accept(this);
}
