namespace Cyborg.Core.Services.Dispatch;

public sealed record ChildProcessResult(int ExitCode, string? StandardOutput, string? StandardError);
