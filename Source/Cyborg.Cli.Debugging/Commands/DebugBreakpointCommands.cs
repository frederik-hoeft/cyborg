using ConsoleAppFramework;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Debugging.Breakpoints;

namespace Cyborg.Cli.Debugging.Commands;

internal sealed class DebugBreakpointCommands(IDebugPauseContext context, IDebugReplIo io)
{
    private readonly IDebugPauseContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly IDebugReplIo _io = io ?? throw new ArgumentNullException(nameof(io));

    /// <summary>Add a persistent breakpoint expression.</summary>
    /// <param name="expression">Regular expression matched against module id, name, and group.</param>
    [Command("break at|b at")]
    public void Add([Argument] params string[] expression)
    {
        if (expression.Length == 0)
        {
            _io.WriteLine("A breakpoint expression is required.", DebugReplOutputKind.Error);
            return;
        }

        string breakpointExpression = string.Join(' ', expression);
        try
        {
            int id = _context.Breakpoints.Add(breakpointExpression);
            _io.WriteLine($"Breakpoint {id} set: {breakpointExpression}", DebugReplOutputKind.Success);
        }
        catch (ArgumentException exception)
        {
            _io.WriteLine($"Invalid breakpoint expression: {exception.Message}", DebugReplOutputKind.Error);
        }
    }

    /// <summary>List registered breakpoints.</summary>
    [Command("break ls|break list|b ls|b list")]
    public void List()
    {
        IReadOnlyList<BreakpointExpression> breakpoints = _context.Breakpoints.List();
        if (breakpoints.Count == 0)
        {
            _io.WriteLine("No breakpoints set.", DebugReplOutputKind.Status);
            return;
        }

        foreach (BreakpointExpression breakpoint in breakpoints)
        {
            _io.WriteLine(breakpoint.ToString());
        }
    }

    /// <summary>Remove a breakpoint by its numeric id.</summary>
    /// <param name="id">Breakpoint id shown by <c>break ls</c>.</param>
    [Command("break rm|break remove|b rm|b remove")]
    public void Remove([Argument] int id)
    {
        if (_context.Breakpoints.Remove(id))
        {
            _io.WriteLine($"Removed breakpoint {id}.", DebugReplOutputKind.Success);
        }
        else
        {
            _io.WriteLine($"No breakpoint with number {id}.", DebugReplOutputKind.Warning);
        }
    }
}
