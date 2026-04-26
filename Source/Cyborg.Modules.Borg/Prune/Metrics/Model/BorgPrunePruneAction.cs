namespace Cyborg.Modules.Borg.Prune.Metrics.Model;

public sealed record BorgPrunePruneAction(int PruneIndex, int PruneTotal) : BorgPruneAction(BorgPruneActionType.Prune);
