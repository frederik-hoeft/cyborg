namespace Cyborg.Core.Parsing.Parsers;

public interface IParser<TParser> : IParser where TParser : class, IParser<TParser>
{
    static abstract TParser Instance { get; }
}
