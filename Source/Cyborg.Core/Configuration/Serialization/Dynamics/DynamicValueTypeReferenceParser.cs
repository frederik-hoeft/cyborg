using System.Collections.Immutable;

namespace Cyborg.Core.Configuration.Serialization.Dynamics;

public static class DynamicValueTypeReferenceParser
{
    public static DynamicValueTypeReference Parse(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        int offset = 0;
        DynamicValueTypeReference result = ParseType(input, ref offset);
        SkipWhitespace(input, ref offset);
        if (offset != input.Length)
        {
            throw new FormatException($"Unexpected trailing characters in dynamic type '{input}' at offset {offset}.");
        }
        return result;
    }

    private static DynamicValueTypeReference ParseType(string input, ref int offset)
    {
        SkipWhitespace(input, ref offset);

        string typeName = ParseIdentifier(input, ref offset);
        SkipWhitespace(input, ref offset);

        if (offset >= input.Length || input[offset] != '<')
        {
            return new DynamicValueTypeReference(typeName, []);
        }

        offset++; // '<'
        ImmutableArray<DynamicValueTypeReference>.Builder typeArguments = ImmutableArray.CreateBuilder<DynamicValueTypeReference>();

        while (true)
        {
            DynamicValueTypeReference typeArgument = ParseType(input, ref offset);
            typeArguments.Add(typeArgument);
            SkipWhitespace(input, ref offset);

            if (offset >= input.Length)
            {
                throw new FormatException($"Unterminated generic argument list in dynamic type '{input}'.");
            }

            if (input[offset] == '>')
            {
                offset++;
                break;
            }

            if (input[offset] != ',')
            {
                throw new FormatException($"Expected ',' or '>' in dynamic type '{input}' at offset {offset}.");
            }

            offset++;
        }

        return new DynamicValueTypeReference(typeName, typeArguments.ToImmutable());
    }

    private static string ParseIdentifier(string input, ref int offset)
    {
        int start = offset;

        while (offset < input.Length)
        {
            char current = input[offset];
            if (current is '<' or '>' or ',' || char.IsWhiteSpace(current))
            {
                break;
            }

            offset++;
        }

        if (offset == start)
        {
            throw new FormatException($"Expected dynamic type name in '{input}' at offset {offset}.");
        }

        return input[start..offset];
    }

    private static void SkipWhitespace(string input, ref int offset)
    {
        while (offset < input.Length && char.IsWhiteSpace(input[offset]))
        {
            offset++;
        }
    }
}