namespace Cyborg.Core.Logging;

public sealed class Logger : ILogger
{
    private readonly List<ILogSink> _sinks;

    public Logger(IEnumerable<ILogSink> sinks)
    {
        _sinks = sinks.ToList();
    }

    public void Log(LogLevel level, string message)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, message);
        foreach (var sink in _sinks)
        {
            sink.Write(entry);
        }
    }

    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warn(string message) => Log(LogLevel.Warn, message);
    public void Error(string message) => Log(LogLevel.Error, message);
    public void Exec(string message) => Log(LogLevel.Exec, message);
}
