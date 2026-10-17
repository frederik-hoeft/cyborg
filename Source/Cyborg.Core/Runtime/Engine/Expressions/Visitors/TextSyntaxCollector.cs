using Cyborg.Core.Runtime.Engine.Expressions.Syntax;

namespace Cyborg.Core.Runtime.Engine.Expressions.Visitors;

internal sealed class TextSyntaxCollector : ITextSyntaxVisitor
{
    private readonly List<TextPartSyntax> _parts = [];

    public IReadOnlyList<TextPartSyntax> Parts => _parts;

    public void Visit(TextPartSyntax part) => _parts.Add(part);
}
