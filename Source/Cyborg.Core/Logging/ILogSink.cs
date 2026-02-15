namespace Cyborg.Core.Logging;

public interface ILogSink
{
    void Write(LogEntry entry);
    void Flush();
}
