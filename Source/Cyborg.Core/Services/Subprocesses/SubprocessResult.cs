namespace Cyborg.Core.Services.Subprocesses;

public sealed record SubprocessResult(int ExitCode, string? StandardOutput, string? StandardError);
