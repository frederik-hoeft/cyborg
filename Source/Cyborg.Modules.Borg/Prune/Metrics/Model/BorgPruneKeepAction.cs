namespace Cyborg.Modules.Borg.Prune.Metrics.Model;

public sealed record BorgPruneKeepAction(string RuleName, int RuleIndex) : BorgPruneAction(BorgPruneActionType.Keep);
