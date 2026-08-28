using ConsoleAppFramework;
using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;

namespace Cyborg.Cli.Debugging.Commands;

internal sealed class DebugBreakpointCommands(IDebugPauseContext context, IDebugReplIo io)
{
    /// <summary>Add a persistent breakpoint expression.</summary>
    /// <param name="expression">Regular expression matched against module id, name, and group.</param>
    [Command("break at|b at")]
    public async Task AddAsync(CancellationToken cancellationToken, [Argument] params string[] expression)
    {
        if (expression.Length == 0)
        {
            await io.WriteLineAsync("A breakpoint expression is required.", OutputKind.Error, cancellationToken);
            return;
        }

        string breakpointExpression = string.Join(' ', expression);
        try
        {
            int id = context.Breakpoints.Add(breakpointExpression);
            await io.WriteLineAsync($"Breakpoint {id} set: {breakpointExpression}", OutputKind.Success, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            await io.WriteLineAsync($"Invalid breakpoint expression: {exception.Message}", OutputKind.Error, cancellationToken);
        }
    }

    /// <summary>List registered breakpoints.</summary>
    [Command("break ls|break list|b ls|b list")]
    public async Task ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<BreakpointExpression> breakpoints = context.Breakpoints.ToList();
        if (breakpoints.Count == 0)
        {
            await io.WriteLineAsync("No breakpoints set.", OutputKind.Status, cancellationToken);
            return;
        }

        foreach (BreakpointExpression breakpoint in breakpoints)
        {
            await io.WriteLineAsync(breakpoint.ToString(), OutputKind.Text, cancellationToken);
        }
    }

    /// <summary>Remove a breakpoint by its numeric id.</summary>
    /// <param name="id">Breakpoint id shown by <c>break ls</c>.</param>
    [Command("break rm|break remove|b rm|b remove")]
    public async Task RemoveAsync([Argument] int id, CancellationToken cancellationToken)
    {
        if (context.Breakpoints.Remove(id))
        {
            await io.WriteLineAsync($"Removed breakpoint {id}.", OutputKind.Success, cancellationToken);
        }
        else
        {
            await io.WriteLineAsync($"No breakpoint with number {id}.", OutputKind.Warning, cancellationToken);
        }
    }
}
