using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class KnownSymbolFormats
{
    public static SymbolDisplayFormat Nullable { get; } = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static SymbolDisplayFormat NonNullable { get; } = SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
}
