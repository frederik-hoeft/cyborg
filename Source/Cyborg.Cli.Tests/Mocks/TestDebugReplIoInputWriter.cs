using System.Text;

namespace Cyborg.Cli.Tests.Mocks;

internal sealed class TestDebugReplIoInputWriter : IDisposable
{
    private readonly MemoryStream _stream = new();

    public TextReader Input => field ??= new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);

    private TextWriter Writer => field ??= new StreamWriter(_stream, Encoding.UTF8, leaveOpen: true);

    public void Write(string text)
    {
        _stream.Position = 0;
        Writer.Write(text);
        Writer.Flush();
        _stream.Position = 0;
    }

    public void Dispose() => _stream.Dispose();
}
