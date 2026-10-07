namespace Cyborg.Core.Runtime.Engine.Environments;

/// <summary>
/// A resolved environment value. Terminal values are snapshots and must not be interpolated or finalized again.
/// </summary>
internal readonly record struct Evaluation(object? Value, bool Terminal)
{
    public static Evaluation Of(object? value) => new(value, Terminal: false);

    public static Evaluation TerminalValue(object? value) => new(value, Terminal: true);

    public Evaluation WithValue(object? value) => new(value, Terminal);
}
