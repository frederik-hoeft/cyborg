using System.Text.RegularExpressions;

namespace Cyborg.Core.Parsing.Parsers;

public interface IRegexOwner
{
    static abstract Regex ParserRegex { get; }
}
