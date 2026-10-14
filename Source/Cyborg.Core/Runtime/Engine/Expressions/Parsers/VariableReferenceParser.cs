using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;

namespace Cyborg.Core.Runtime.Engine.Expressions.Parsers;

/// <summary>Parses the existing identifier, contextual self, and entry-point reference syntax.</summary>
internal sealed class VariableReferenceParser(bool allowEntryPoint = true, string? name = null) : ParserBase
{
    public override string? Name => name;

    public override IParser NamedCopy(string newName) => new VariableReferenceParser(allowEntryPoint, newName);

    public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offset, (uint)input.Length);

        int position = offset;
        ReferenceAnchor anchor = ReferenceAnchor.CurrentScope;
        if (allowEntryPoint && position < input.Length && input[position] == '@')
        {
            position++;
            anchor = ReferenceAnchor.EntryPoint;
        }

        string? identifier;
        if (position < input.Length && input[position] == '@')
        {
            position++;
            identifier = null;
        }
        else if (TryReadIdentifier(input, position, out int end))
        {
            identifier = input[position..end];
            position = end;
        }
        else if (anchor == ReferenceAnchor.EntryPoint)
        {
            // A solitary '@' is current-scope self; '@@' is entry-point self.
            identifier = null;
            anchor = ReferenceAnchor.CurrentScope;
        }
        else
        {
            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }

        syntaxNode = new VariableReferenceNode(new VariableReferenceSyntax(identifier, anchor));
        charsConsumed = position - offset;
        return true;
    }

    private static bool TryReadIdentifier(string input, int offset, out int end)
    {
        end = offset;
        if (offset >= input.Length || !IsIdentifierStart(input[offset]))
        {
            return false;
        }
        end++;
        while (end < input.Length && IsIdentifierPart(input[end]))
        {
            end++;
        }
        while (end + 1 < input.Length && input[end] == '.' && IsIdentifierPart(input[end + 1]))
        {
            end += 2;
            while (end < input.Length && IsIdentifierPart(input[end]))
            {
                end++;
            }
        }
        return true;
    }

    private static bool IsIdentifierStart(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_' or '-';

    private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || c is >= '0' and <= '9';
}
