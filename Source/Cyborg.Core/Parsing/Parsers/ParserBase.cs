using System.Diagnostics.CodeAnalysis;
using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Parsing.Visitors;

namespace Cyborg.Core.Parsing.Parsers;

public abstract class ParserBase : IParser
{
    public abstract string? Name { get; }

    public virtual bool CanAccept(Type visitorType)
    {
        ArgumentNullException.ThrowIfNull(visitorType);
        return typeof(INodeVisitor).IsAssignableFrom(visitorType);
    }

    public abstract IParser NamedCopy(string name);

    public abstract bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed);
}
