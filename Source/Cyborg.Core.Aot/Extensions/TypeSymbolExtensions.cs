using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Extensions;

internal static class TypeSymbolExtensions
{
    extension(ITypeSymbol self)
    {
        /// <summary>
        /// Attempts to unwrap a nullable type, returning the underlying type if successful.
        /// </summary>
        /// <remarks>This method checks if the current type is annotated as nullable or if it is a
        /// nullable type. If so, it provides the underlying type without the nullable annotation.</remarks>
        /// <param name="unwrapped">When the method returns <see langword="true"/>, contains the unwrapped underlying type symbol. Otherwise,
        /// contains the original type symbol.</param>
        /// <returns>true if the type was successfully unwrapped; otherwise, false.</returns>
        public bool TryUnwrapNullableType(out ITypeSymbol unwrapped)
        {
            // need to actually unwrap nullable value types, rather than just removing the nullable annotation, so check that first
            if (self is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                unwrapped = namedType.TypeArguments[0];
                return true;
            }
            if (self.NullableAnnotation == NullableAnnotation.Annotated)
            {
                unwrapped = self.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                return true;
            }
            unwrapped = self;
            return false;
        }

        public bool EqualsIgnoreNullability(SpecialType specialType)
        {
            _ = self.TryUnwrapNullableType(out ITypeSymbol unwrapped);
            return unwrapped.SpecialType == specialType;
        }

        /// <summary>
        /// Determines whether the type, after removing nullable wrapping/annotation, matches <paramref name="expectedType"/>.
        /// </summary>
        public bool EqualsIgnoreNullability(ITypeSymbol expectedType)
        {
            _ = self.TryUnwrapNullableType(out ITypeSymbol unwrapped);
            return SymbolEqualityComparer.Default.Equals(unwrapped, expectedType);
        }

        /// <summary>
        /// Determines whether the type is either a CLR string or the configured tagged-string type.
        /// </summary>
        public bool IsStringLike(ITypeSymbol taggedStringType) =>
            self.EqualsIgnoreNullability(SpecialType.System_String) || self.EqualsIgnoreNullability(taggedStringType);

        /// <summary>
        /// Determines whether this type can ever be null, i.e., whether generated code should emit a null check for it.
        /// </summary>
        public bool CanEverBeNull => self
            is { IsReferenceType: true }
            or INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
            // catch-all for unbound generic type parameters: T?
            or { NullableAnnotation: NullableAnnotation.Annotated };
    }
}
