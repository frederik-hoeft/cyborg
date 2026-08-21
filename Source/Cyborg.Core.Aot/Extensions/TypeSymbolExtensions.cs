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

        /// <summary>
        /// Determines whether the type is <see cref="string"/>, ignoring nullable annotations.
        /// </summary>
        public bool IsStringType()
        {
            _ = self.TryUnwrapNullableType(out ITypeSymbol unwrapped);
            return unwrapped.SpecialType == SpecialType.System_String;
        }

        /// <summary>
        /// Determines whether the type, after removing nullable wrapping/annotation, matches <paramref name="expectedType"/>.
        /// </summary>
        public bool IsOrNullableOf(ITypeSymbol expectedType)
        {
            _ = self.TryUnwrapNullableType(out ITypeSymbol unwrapped);
            return SymbolEqualityComparer.Default.Equals(unwrapped, expectedType);
        }

        /// <summary>
        /// Determines whether the type is either a CLR string or the configured tagged-string type.
        /// </summary>
        public bool IsStringLike(ITypeSymbol taggedStringType) =>
            self.IsStringType() || self.IsOrNullableOf(taggedStringType);

        /// <summary>
        /// Determines whether generated code should guard the value against null before dereferencing it.
        /// </summary>
        public bool RequiresNullCheck()
        {
            if (self is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
            {
                return true;
            }
            return !self.IsValueType
                && (self.IsReferenceType || self.NullableAnnotation == NullableAnnotation.Annotated);
        }
    }
}
