namespace Cyborg.Core.Parsing.Parsers;

public sealed class Optional<TParser>() : Optional(TParser.Instance), IParser<Optional<TParser>> where TParser : class, IParser<TParser>
{
    public static Optional<TParser> Instance { get; } = new();
}
