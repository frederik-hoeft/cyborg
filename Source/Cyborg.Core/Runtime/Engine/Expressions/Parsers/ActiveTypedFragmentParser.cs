using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Visitors;

namespace Cyborg.Core.Runtime.Engine.Expressions.Parsers;

/// <summary>Identifies active typed fragments so the enclosing text grammar rejects embedded or malformed typed expressions.</summary>
internal sealed class ActiveTypedFragmentParser(string? name = null) : ParserBase<ITextSyntaxVisitor>
{
    public override string? Name => name;

    public override IParser NamedCopy(string newName) => new ActiveTypedFragmentParser(newName);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offset, (uint)input.Length);
        syntaxNode = null;
        charsConsumed = 0;
        if (offset + 2 >= input.Length || input[offset] is not ('&' or '*') || input[offset + 1] != '{' || input[offset + 2] == '#')
        {
            return false;
        }
        int closing = input.IndexOf('}', offset + 2);
        if (closing < 0)
        {
            return false;
        }
        charsConsumed = closing + 1 - offset;
        syntaxNode = new RejectedTypedFragmentNode(input.Substring(offset, charsConsumed));
        return true;
    }
}
