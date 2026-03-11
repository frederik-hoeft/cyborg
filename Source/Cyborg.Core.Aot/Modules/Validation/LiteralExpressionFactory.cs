using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class LiteralExpressionFactory
{
    public static bool TryGetLiteralExpression(TypedConstant constant, ITypeSymbol targetType, [NotNullWhen(true)] out string? expression)
    {
        expression = constant switch
        {
            { IsNull: true } or { Value: null } => "null",
            { Value: { } value } => targetType switch
            {
                { SpecialType: SpecialType.System_String } => SymbolDisplay.FormatLiteral((string)value, quote: true),
                { SpecialType: SpecialType.System_Char } => SymbolDisplay.FormatLiteral((char)value, quote: true),
                { SpecialType: SpecialType.System_Boolean } => (bool)value ? "true" : "false",
                { SpecialType: SpecialType.System_Byte } => $"(byte){((byte)value).ToString(CultureInfo.InvariantCulture)}",
                { SpecialType: SpecialType.System_SByte } => $"(sbyte){((sbyte)value).ToString(CultureInfo.InvariantCulture)}",
                { SpecialType: SpecialType.System_Int16 } => $"(short){((short)value).ToString(CultureInfo.InvariantCulture)}",
                { SpecialType: SpecialType.System_UInt16 } => $"(ushort){((ushort)value).ToString(CultureInfo.InvariantCulture)}",
                { SpecialType: SpecialType.System_Int32 } => ((int)value).ToString(CultureInfo.InvariantCulture),
                { SpecialType: SpecialType.System_UInt32 } => $"{((uint)value).ToString(CultureInfo.InvariantCulture)}U",
                { SpecialType: SpecialType.System_Int64 } => $"{((long)value).ToString(CultureInfo.InvariantCulture)}L",
                { SpecialType: SpecialType.System_UInt64 } => $"{((ulong)value).ToString(CultureInfo.InvariantCulture)}UL",
                { SpecialType: SpecialType.System_Single } => $"{((float)value).ToString("R", CultureInfo.InvariantCulture)}F",
                { SpecialType: SpecialType.System_Double } => $"{((double)value).ToString("R", CultureInfo.InvariantCulture)}D",
                { SpecialType: SpecialType.System_Decimal } => $"{((decimal)value).ToString(CultureInfo.InvariantCulture)}M",
                { TypeKind: TypeKind.Enum } => $"({targetType.ToDisplayString(KnownSymbolFormats.Nullable)}){Convert.ToString(value, CultureInfo.InvariantCulture)}",
                _ => null,
            }
        };
        return expression is not null;
    }
}
