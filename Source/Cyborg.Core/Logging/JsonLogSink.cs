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
        // Format matches the bash script output format for compatibility
        // Example: "Sun Feb  1 04:25:01 AM CET 2026"
        var jsonEntry = new JsonLogEntry(
            entry.Timestamp.ToString("ddd MMM dd hh:mm:ss tt zzz yyyy"),
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
