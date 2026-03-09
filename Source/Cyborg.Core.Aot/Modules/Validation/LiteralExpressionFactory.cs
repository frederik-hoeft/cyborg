using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class LiteralExpressionFactory
{
    public static string GetDefaultEqualityComparer(string typeName) =>
        $"""
        global::{typeof(EqualityComparer<>).Namespace}.{nameof(EqualityComparer<>)}<{typeName}>.{nameof(EqualityComparer<>.Default)}
        """;

    public static string GetTypeNameBase(Type t) => $"global::{t.Namespace}.{t.Name}";

    public static bool TryGetLiteralExpression(TypedConstant constant, ITypeSymbol targetType, out string? expression)
    {
        expression = null;

        if (constant.IsNull)
        {
            expression = "null";
            return true;
        }

        object? value = constant.Value;
        if (value is null)
        {
            expression = "null";
            return true;
        }

        SpecialType specialType = targetType.SpecialType;
        string targetTypeName = targetType.ToDisplayString(GenerationCandidateFactory.s_fullyQualifiedNullableFormat);

        switch (specialType)
        {
            case SpecialType.System_String:
                expression = SymbolDisplay.FormatLiteral((string)value, quote: true);
                return true;
            case SpecialType.System_Char:
                expression = SymbolDisplay.FormatLiteral((char)value, quote: true);
                return true;
            case SpecialType.System_Boolean:
                expression = (bool)value ? "true" : "false";
                return true;
            case SpecialType.System_Byte:
                expression = "(byte)" + ((byte)value).ToString(CultureInfo.InvariantCulture);
                return true;
            case SpecialType.System_SByte:
                expression = "(sbyte)" + ((sbyte)value).ToString(CultureInfo.InvariantCulture);
                return true;
            case SpecialType.System_Int16:
                expression = "(short)" + ((short)value).ToString(CultureInfo.InvariantCulture);
                return true;
            case SpecialType.System_UInt16:
                expression = "(ushort)" + ((ushort)value).ToString(CultureInfo.InvariantCulture);
                return true;
            case SpecialType.System_Int32:
                expression = ((int)value).ToString(CultureInfo.InvariantCulture);
                return true;
            case SpecialType.System_UInt32:
                expression = ((uint)value).ToString(CultureInfo.InvariantCulture) + "U";
                return true;
            case SpecialType.System_Int64:
                expression = ((long)value).ToString(CultureInfo.InvariantCulture) + "L";
                return true;
            case SpecialType.System_UInt64:
                expression = ((ulong)value).ToString(CultureInfo.InvariantCulture) + "UL";
                return true;
            case SpecialType.System_Single:
                expression = ((float)value).ToString("R", CultureInfo.InvariantCulture) + "F";
                return true;
            case SpecialType.System_Double:
                expression = ((double)value).ToString("R", CultureInfo.InvariantCulture) + "D";
                return true;
            case SpecialType.System_Decimal:
                expression = ((decimal)value).ToString(CultureInfo.InvariantCulture) + "M";
                return true;
        }

        if (targetType.TypeKind == TypeKind.Enum)
        {
            expression = $"({targetTypeName}){Convert.ToString(value, CultureInfo.InvariantCulture)}";
            return true;
        }

        return false;
    }
}
