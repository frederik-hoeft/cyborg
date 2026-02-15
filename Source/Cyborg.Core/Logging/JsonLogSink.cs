using System.Text.Json;

namespace Cyborg.Core.Logging;

public sealed class JsonLogSink : ILogSink
{
    private readonly TextWriter _writer;

    public JsonLogSink(TextWriter writer)
    {
        _writer = writer;
    }

    public void Write(LogEntry entry)
    {
        var jsonEntry = new JsonLogEntry(
            entry.Timestamp.ToString("ddd MMM  d hh:mm:ss tt zzz yyyy"),
            entry.Level.ToString().ToUpperInvariant(),
            entry.Message,
            entry.Exception
        );
        
        var json = JsonSerializer.Serialize(jsonEntry, CyborgJsonContext.Default.JsonLogEntry);
        _writer.WriteLine(json);
    }

    public void Flush()
    {
        _writer.Flush();
    }
}
