using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;

namespace Cyborg.Core.Runtime.Engine.Expressions.Parsers;

/// <summary>Parses a complete lazy typed reference; the enclosing grammar enforces whole-value positioning.</summary>
internal sealed class TypedReferenceParser(string? name = null) : ParserBase, IParser<TypedReferenceParser>
{
    public static TypedReferenceParser Instance { get; } = new();

    private readonly VariableReferenceParser _reference = VariableReferenceParser.Instance;

    public override string? Name => name;

    public override IParser NamedCopy(string newName) => new TypedReferenceParser(newName);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offset, (uint)input.Length);
        syntaxNode = null;
        charsConsumed = 0;
        if (!input.AsSpan(offset).StartsWith("&{"))
        {
            return false;
        }
        int referenceOffset = offset + 2;
        if (!_reference.TryParse(input, referenceOffset, out ISyntaxNode? node, out int referenceLength)
            || node is not VariableReferenceNode reference
            || referenceOffset + referenceLength >= input.Length
            || input[referenceOffset + referenceLength] != '}')
        {
            return false;
        }
        syntaxNode = new ValueNode(new TypedReferenceSyntax(reference.Evaluate()));
        charsConsumed = 3 + referenceLength;
        return true;
    }
}
