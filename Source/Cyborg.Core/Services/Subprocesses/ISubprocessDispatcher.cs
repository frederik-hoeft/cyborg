using System.Diagnostics;

namespace Cyborg.Core.Services.Subprocesses;

public interface ISubprocessDispatcher
{
    Task<SubprocessResult> ExecuteAsync(ProcessStartInfo processStartInfo, CancellationToken cancellationToken);
}
