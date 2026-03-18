using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Modules.Borg.Prune.Metrics.Model;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Prune.Metrics.SyntaxNodes;

public sealed class KeepArchiveSyntaxNode(string? name, BorgPruneKeepAction result) : SyntaxNode<BorgPruneVisitor, BorgPruneKeepAction>(name, result)
{
    protected override void Accept([NotNull] BorgPruneVisitor visitor) => visitor.Accept(this);
}
