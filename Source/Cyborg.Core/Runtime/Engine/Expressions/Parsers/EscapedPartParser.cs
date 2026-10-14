using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Visitors;

namespace Cyborg.Core.Runtime.Engine.Expressions.Parsers;

/// <summary>Recognizes escaped interpolation, indirection, and capture tokens without consuming escape layers.</summary>
internal sealed class EscapedPartParser(string? name = null) : ParserBase<ITextSyntaxVisitor>
{
    public override string? Name => name;

    public override IParser NamedCopy(string newName) => new EscapedPartParser(newName);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offset, (uint)input.Length);
        syntaxNode = null;
        charsConsumed = 0;
        if (offset + 3 >= input.Length || input[offset] is not ('$' or '&' or '*') || input[offset + 1] != '{' || input[offset + 2] != '#')
        {
            return false;
        }
        int hashesEnd = offset + 3;
        while (hashesEnd < input.Length && input[hashesEnd] == '#')
        {
            hashesEnd++;
        }
        int closing = input.IndexOf('}', hashesEnd);
        if (closing < 0)
        {
            return false;
        }
        syntaxNode = new TextPartNode(new EscapedPartSyntax(input[offset], hashesEnd - offset - 2, input[hashesEnd..closing]));
        charsConsumed = closing + 1 - offset;
        return true;
    }
}
