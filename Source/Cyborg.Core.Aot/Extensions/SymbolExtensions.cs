using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Extensions;

internal static class SymbolExtensions
{
    extension(ISymbol self)
    {
        /// <summary>
        /// Determines whether the current object has an attribute of the specified type.
        /// </summary>
        /// <remarks>This method iterates through the attributes associated with the current object and
        /// checks for the presence of the specified attribute type. It performs a case-sensitive comparison of the
        /// attribute type's full name.</remarks>
        /// <typeparam name="T">The type of the attribute to check for. This type must derive from the base class Attribute.</typeparam>
        /// <returns>true if the specified attribute type is present; otherwise, false.</returns>
        public bool HasAttribute<T>() where T : Attribute
        {
            foreach (AttributeData attribute in self.GetAttributes())
            {
                if (attribute.AttributeClass is { } attributeClass && attributeClass.GetFullMetadataName().Equals(typeof(T).FullName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
