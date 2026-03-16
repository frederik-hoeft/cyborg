using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Parsing.Parsers;

public abstract class ParserBase(string? name) : IParser
{
    public string? Name { get; init; } = name;

    public abstract bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed);

    public abstract IParser NamedCopy(string name);
}

