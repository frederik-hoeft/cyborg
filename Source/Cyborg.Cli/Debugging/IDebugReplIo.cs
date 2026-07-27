namespace Cyborg.Cli.Debugging;

/// <summary>
/// Abstraction over console I/O for the debug REPL, enabling tests to feed scripted input
/// without blocking on a real terminal.
/// </summary>
public interface IDebugReplIo
{
    void WriteLine(string message);

    void Write(string message);

    /// <summary>
    /// Reads the next line of user input, or null on EOF.
    /// </summary>
    string? ReadLine();
}

internal sealed class ConsoleDebugReplIo : IDebugReplIo
{
    public void WriteLine(string message) => Console.Out.WriteLine(message);

    public void Write(string message) => Console.Out.Write(message);

    public string? ReadLine() => Console.In.ReadLine();
}

/// <summary>
/// Scripted I/O for tests and unattended automation.
/// </summary>
internal sealed class TextDebugReplIo(TextReader input, TextWriter output) : IDebugReplIo
{
    public void WriteLine(string message) => output.WriteLine(message);

    public void Write(string message) => output.Write(message);

    public string? ReadLine() => input.ReadLine();
}
