using System.Text;

namespace Cyborg.Core.Aot.Extensions;

internal static class SymbolNameGenerator
{
    /// <summary>
    /// Generates a unique symbol name based on the provided name.
    /// </summary>
    public static string MakeUnique(string name) => name switch
    {
        not { Length: > 0 } => $"Local_{Guid.NewGuid():N}__generated",
        _ => $"{name}_{Guid.NewGuid():N}__generated",
    };

    public static string MakeCamelCase(string name) => name switch
    {
        not { Length: > 0 } => string.Empty,
        { Length: 1 } => name.ToLowerInvariant(),
        _ => $"{char.ToLowerInvariant(name[0])}{name[1..]}",
    };

    public static string CreateSafeIdentifier(string value)
    {
        StringBuilder builder = new();
        for (int i = 0; i < value.Length; ++i)
        {
            char c = value[i];
            if (i == 0 && char.IsDigit(c))
            {
                builder.Append('_');
            }
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else
            {
                builder.Append('_');
            }
        }
        return builder.ToString();
    }
}
