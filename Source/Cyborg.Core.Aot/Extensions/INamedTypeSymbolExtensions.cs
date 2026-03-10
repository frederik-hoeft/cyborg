using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Extensions;

internal static class INamedTypeSymbolExtensions
{
    extension (INamedTypeSymbol self)
    {
        /// <summary>
        /// Generates a string representation of the current object, including the fully qualified name with the global
        /// namespace.
        /// </summary>
        /// <returns>A string that represents the fully qualified name of the current object, formatted according to the
        /// specified display format.</returns>
        public string RenderGlobal() => self.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included));

        /// <summary>
        /// Generates a fully qualified display string for a generic type, substituting the type parameters with the
        /// provided type arguments.
        /// </summary>
        /// <remarks>This method is intended for use with generic types only. If no type arguments are
        /// provided, the method returns the display string with the type parameters included.</remarks>
        /// <param name="typeArgs">The type arguments to substitute for the generic parameters of the type. Must match the number of generic
        /// parameters defined for the type.</param>
        /// <returns>A string representing the fully qualified name of the generic type with the specified type arguments
        /// applied.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the type is not a generic type or if the number of provided type arguments does not match the
        /// number of generic parameters.</exception>
        public string RenderGlobalWithGenerics(string typeArg, params string[] typeArgs)
        {
            List<string> allTypeArgs = [typeArg, ..typeArgs];
            if (!self.IsGenericType)
            {
                throw new InvalidOperationException($"Expected a generic type symbol, but got '{self.Name}' which is not generic.");
            }
            if (allTypeArgs.Count != self.TypeParameters.Length)
            {
                throw new InvalidOperationException($"The number of provided type arguments ({allTypeArgs.Count}) does not match the number of generic parameters ({self.TypeParameters.Length}) for the type '{self.Name}'.");
            }
            return $"global::{self.GetFullMetadataName().Replace($"`{self.TypeParameters.Length}", $"<{string.Join(", ", allTypeArgs)}>")}";
        }

        /// <summary>
        /// Gets the fully qualified metadata name of the type, including its namespace and any generic type parameters.
        /// </summary>
        /// <remarks>This method traverses the containing symbols of the type to construct the full
        /// metadata name, starting from the innermost type and proceeding outward to the namespace. The resulting name
        /// uniquely identifies the type within its assembly and includes generic arity information when
        /// relevant.</remarks>
        /// <returns>A string representing the complete metadata name of the type, formatted as a dot-separated list of
        /// namespaces and type names. Generic type parameters are included in the name if applicable.</returns>
        public string GetFullMetadataName()
        {
            INamedTypeSymbol original = self.OriginalDefinition;

            Stack<string> parts = [];
            ISymbol? current = original;

            while (current is not null)
            {
                switch (current)
                {
                    case INamespaceSymbol ns when !ns.IsGlobalNamespace:
                        parts.Push(ns.MetadataName);
                        break;
                    case INamedTypeSymbol named:
                        parts.Push(named.MetadataName); // includes `1, `2, etc.
                        break;
                }

                current = current.ContainingSymbol;
            }

            return string.Join(".", parts);
        }
    }
}
