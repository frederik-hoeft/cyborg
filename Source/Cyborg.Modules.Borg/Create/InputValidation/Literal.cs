using Cyborg.Core.Parsing.Parsers;
using Cyborg.Core.Parsing.SyntaxNodes;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Create.InputValidation;

internal sealed class Literal(string value, string? name = null) : IParser
{
    public string? Name => name;

    public string Value => value;

    public IParser NamedCopy(string name) => new Literal(Value, name);

    public bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
    {
        ReadOnlySpan<char> inputSpan = input.AsSpan(offset);
        if (inputSpan.StartsWith(Value, StringComparison.Ordinal))
        {
            charsConsumed = Value.Length;
            syntaxNode = new LiteralSyntaxNode(Name, Value);
            return true;
        }
        charsConsumed = 0;
        syntaxNode = null;
        return false;
    }
}