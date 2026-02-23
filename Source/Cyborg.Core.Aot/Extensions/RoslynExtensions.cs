using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Extensions;

/// <summary>
/// Quality-of-life extension methods for Roslyn symbols.
/// </summary>
internal static class RoslynExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol namespaceSymbol)
    {
        // depth-first traversal of the namespace tree to yield all contained type symbols
        Stack<INamespaceSymbol> remaining = [];
        remaining.Push(namespaceSymbol);
        while (remaining.Count > 0)
        {
            namespaceSymbol = remaining.Pop();
            foreach (INamedTypeSymbol type in namespaceSymbol.GetTypeMembers())
            {
                yield return type;
            }
            foreach (INamespaceSymbol nestedNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                remaining.Push(nestedNamespace);
            }
        }
    }

    public static bool TryGetValue(this ImmutableArray<KeyValuePair<string, TypedConstant>> source, string key, out TypedConstant value)
    {
        foreach (KeyValuePair<string, TypedConstant> pair in source)
        {
            if (pair.Key == key)
            {
                value = pair.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}