using ConsoleAppFramework;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// ConsoleAppFramework adapter for debugger commands. CAF owns generated grammar/routing; this type owns the framework's process-wide hooks for one dispatch.
/// </summary>
internal sealed class CafDebugCommandRouter
{
    private static readonly SemaphoreSlim s_dispatchGate = new(initialCount: 1, maxCount: 1);
    private readonly ConsoleApp.ConsoleAppBuilder _app;

    public CafDebugCommandRouter()
    {
        _app = ConsoleApp.Create();
        DebugCommandRegistration.Register(_app);
    }

    internal async ValueTask RunAsync(string[] arguments, IServiceProvider services, IDebugReplIo io, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(io);
        cancellationToken.ThrowIfCancellationRequested();

        arguments = RewriteHelpCommand(arguments);
        await s_dispatchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        IServiceProvider? originalServiceProvider = ConsoleApp.ServiceProvider;
        Action<string> originalLog = ConsoleApp.Log;
        Action<string> originalLogError = ConsoleApp.LogError;
        int originalExitCode = Environment.ExitCode;

        ConsoleApp.ServiceProvider = services;
        ConsoleApp.Log = message => io.WriteLine(message, DebugReplOutputKind.Status);
        ConsoleApp.LogError = message => io.WriteLine(message, DebugReplOutputKind.Error);

        try
        {
            await _app.RunAsync(arguments, disposeServiceProvider: false, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            ConsoleApp.ServiceProvider = originalServiceProvider;
            ConsoleApp.Log = originalLog;
            ConsoleApp.LogError = originalLogError;
            Environment.ExitCode = originalExitCode;
            s_dispatchGate.Release();
        }
    }

    private static string[] RewriteHelpCommand(string[] arguments)
    {
        if (arguments[0] is not ("help" or "h" or "?"))
        {
            return arguments;
        }
        if (arguments.Length == 1)
        {
            return ["--help"];
        }

        string[] helpArguments = new string[arguments.Length];
        Array.Copy(arguments, 1, helpArguments, 0, arguments.Length - 1);
        helpArguments[^1] = "--help";
        return helpArguments;
    }
}
