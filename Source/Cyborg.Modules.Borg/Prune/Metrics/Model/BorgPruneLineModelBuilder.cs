using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Prune.Metrics.Model;

public sealed class BorgPruneLineModelBuilder
{
    public BorgPruneAction? Action { get; internal set; }

    public string? ArchiveName { get; internal set; }

    public DateTime? ArchiveTimestamp { get; internal set; }

    public string? ArchiveId { get; internal set; }

    public bool TryBuild([NotNullWhen(true)] out BorgPruneLineModel? model)
    {
        if (this is not { Action: not null, ArchiveId.Length: > 0, ArchiveName.Length: > 0, ArchiveTimestamp: { } })
        {
            model = null;
            return false;
        }
        model = new BorgPruneLineModel(Action, ArchiveName, ArchiveTimestamp.Value, ArchiveId);
        return true;
    }
}
