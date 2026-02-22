namespace Cyborg.Core.Logging;

public interface ILogger
{
    void Log(LogLevel level, string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Exec(string message);
}
