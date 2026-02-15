namespace Cyborg.Core.Logging;

public sealed record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    string? Exception = null
);
