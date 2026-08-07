using ConsoleAppFramework;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Modules.Descriptors;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli.Debugging;

internal sealed class DebugCommandDispatcher
{
    private readonly ConsoleApp.ConsoleAppBuilder _app;
    private readonly IDebugReplIo _io;
    private readonly IModuleSerializationService _moduleSerializationService;
    private CancellationToken _cancellationToken;
    private DebugResumeAction? _resumeAction;
    private int _isDispatching;

    public DebugCommandDispatcher(IDebugReplIo io, IModuleSerializationService moduleSerializationService)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(moduleSerializationService);
        _io = io;
        _moduleSerializationService = moduleSerializationService;

        _app = ConsoleApp.Create();
        _app.Add("continue|c|resume", Continue);
        _app.Add("step|s", Step);
        _app.Add("detach", Detach);
        _app.Add("inspect|i", InspectAsync);
        _app.Add("cancel|q|quit", Cancel);
        _app.Add("break at|b at", BreakAt);
        _app.Add("break ls|break list|b ls|b list", BreakList);
        _app.Add("break rm|break remove|b rm|b remove", BreakRemove);
    }

    public async ValueTask<DebugResumeAction?> DispatchAsync(string commandLine, IDebugPauseContext context, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!CommandLineTokenizer.TryTokenize(commandLine, out string[] arguments, out string? error))
        {
            _io.WriteLine(error!);
            return null;
        }

        if (arguments.Length == 0)
        {
            return null;
        }

        arguments = RewriteHelpCommand(arguments);

        if (Interlocked.CompareExchange(ref _isDispatching, value: 1, comparand: 0) != 0)
        {
            throw new InvalidOperationException("Concurrent debugger command dispatch is not supported.");
        }

        Action<string> originalLog = ConsoleApp.Log;
        Action<string> originalLogError = ConsoleApp.LogError;
        int originalExitCode = Environment.ExitCode;

        Context = context;
        _cancellationToken = cancellationToken;
        _resumeAction = null;
        ConsoleApp.Log = _io.WriteLine;
        ConsoleApp.LogError = _io.WriteLine;

        try
        {
            await _app.RunAsync(arguments, disposeServiceProvider: false, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return _resumeAction;
        }
        finally
        {
            ConsoleApp.Log = originalLog;
            ConsoleApp.LogError = originalLogError;
            Environment.ExitCode = originalExitCode;
            _resumeAction = null;
            _cancellationToken = default;
            Context = null;
            Volatile.Write(ref _isDispatching, 0);
        }
    }

    /// <summary>Continue workflow execution until the next breakpoint.</summary>
    private void Continue() => _resumeAction = DebugResumeAction.Continue;

    [AllowNull]
    private IDebugPauseContext Context
    {
        get => field ?? throw new InvalidOperationException("No debugger pause context is active for command execution.");
        set;
    }

    /// <summary>Execute the next module and break again.</summary>
    private void Step()
    {
        Context.RequestStep();
        _resumeAction = DebugResumeAction.Continue;
    }

    /// <summary>Remove all breakpoints and continue workflow execution.</summary>
    private void Detach()
    {
        Context.Detach();
        _resumeAction = DebugResumeAction.Continue;
    }

    /// <summary>Print the full validated state of the current module.</summary>
    private async Task InspectAsync()
    {
        string inspection = await _moduleSerializationService.SerializeAsync(Context.Module.GetDescriptor(), ModuleDescriptionFormats.Text, _cancellationToken);
        _io.WriteLine(inspection);
    }

    /// <summary>Cancel the current module and terminate workflow execution.</summary>
    private void Cancel() => _resumeAction = DebugResumeAction.Cancel;

    /// <summary>Add a persistent breakpoint expression.</summary>
    /// <param name="expression">Regular expression matched against module id, name, and group.</param>
    private void BreakAt([Argument] params string[] expression)
    {
        if (expression.Length == 0)
        {
            _io.WriteLine("A breakpoint expression is required.");
            return;
        }

        string breakpointExpression = string.Join(' ', expression);
        try
        {
            int id = Context.Breakpoints.Add(breakpointExpression);
            _io.WriteLine($"Breakpoint {id} set: {breakpointExpression}");
        }
        catch (ArgumentException exception)
        {
            _io.WriteLine($"Invalid breakpoint expression: {exception.Message}");
        }
    }

    /// <summary>List registered breakpoints.</summary>
    private void BreakList()
    {
        IReadOnlyList<BreakpointExpression> breakpoints = Context.Breakpoints.List();
        if (breakpoints.Count == 0)
        {
            _io.WriteLine("No breakpoints set.");
            return;
        }

        foreach (BreakpointExpression breakpoint in breakpoints)
        {
            _io.WriteLine(breakpoint.ToString());
        }
    }

    /// <summary>Remove a breakpoint by its numeric id.</summary>
    /// <param name="id">Breakpoint id shown by <c>break ls</c>.</param>
    private void BreakRemove([Argument] int id)
    {
        if (Context.Breakpoints.Remove(id))
        {
            _io.WriteLine($"Removed breakpoint {id}.");
        }
        else
        {
            _io.WriteLine($"No breakpoint with number {id}.");
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
