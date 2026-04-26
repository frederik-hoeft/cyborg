using System.Diagnostics;

namespace Cyborg.Core.Services.Dispatch;

public interface IChildProcessDispatcher
{
    Task<ChildProcessResult> ExecuteAsync(ProcessStartInfo processStartInfo, CancellationToken cancellationToken);
}
