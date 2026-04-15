using Cyborg.Core.Parsing.Visitors;
using Cyborg.Modules.Borg.Prune.Metrics.Model;
using Cyborg.Modules.Borg.Prune.Metrics.SyntaxNodes;

namespace Cyborg.Modules.Borg.Prune.Metrics;

public sealed class BorgPruneVisitor(BorgPruneLineModelBuilder model) : INodeVisitor
{
    internal void Accept(KeepArchiveSyntaxNode borgPruneKeepActionSyntaxNode) => model.Action = borgPruneKeepActionSyntaxNode.Evaluate();

    internal void Accept(PruneArchiveSyntaxNode borgPrunePruneActionSyntaxNode) => model.Action = borgPrunePruneActionSyntaxNode.Evaluate();

    internal void Accept(ArchiveNameSyntaxNode archiveNameSyntaxNode) => model.ArchiveName = archiveNameSyntaxNode.Evaluate();

    internal void Accept(ArchiveTimestampSyntaxNode archiveTimestampSyntaxNode) => model.ArchiveTimestamp = archiveTimestampSyntaxNode.Evaluate();

    internal void Accept(ArchiveIdSyntaxNode archiveIdSyntaxNode) => model.ArchiveId = archiveIdSyntaxNode.Evaluate();
}