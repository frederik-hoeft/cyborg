using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Model;
using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DefinedEnumValueAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(DefinedEnumValueAttribute).FullName!;

    public bool TryProcess(PropertyAttributeProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        _ = attribute;
        aspect = null;

        if (!TryGetEnumShape(context.Property.Type, out ITypeSymbol? enumType, out bool isNullableEnum))
        {
            context.Report(
                ValidationGeneratorDiagnostics.UnsupportedDefinedEnumValueTargetType,
                context.Property.Name,
                context.ContainingType.Name,
                context.Property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            return false;
        }

        aspect = new DefinedEnumValueValidationAspect(
            enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            isNullableEnum);

        return true;
    }

    private static bool TryGetEnumShape(ITypeSymbol type, [NotNullWhen(true)] out ITypeSymbol? enumType, out bool isNullableEnum)
    {
        enumType = null;
        isNullableEnum = false;
        if (type.TypeKind == TypeKind.Enum)
        {
            enumType = type;
            return true;
        }
        if (type is INamedTypeSymbol
        {
            IsGenericType: true,
            ConstructedFrom.SpecialType: SpecialType.System_Nullable_T,
            TypeArguments:
            [
                { TypeKind: TypeKind.Enum } nullableEnum
            ]
        })
        {
            enumType = nullableEnum;
            isNullableEnum = true;
            return true;
        }

        return false;
    }

    private sealed class DefinedEnumValueValidationAspect(string enumTypeName, bool isNullableEnum) : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            if (isNullableEnum)
            {
                builder.AppendBlock(
                $$"""
                if ({{model.AccessExpression}} is not null && !{{KnownTypes.Enum}}.IsDefined<{{enumTypeName}}>({{model.AccessExpression}}.Value))
                {
                    errors.Add({{CreateValidationError(model, "enum", $"Property '{{nameof({model.AccessExpression})}}' must be a defined enum value.")}});
                }
                """);

                return;
            }

            builder.AppendBlock(
            $$"""
            if (!{{KnownTypes.Enum}}.IsDefined<{{enumTypeName}}>({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, "enum", $"Property '{{nameof({model.AccessExpression})}}' must be a defined enum value.")}});
            }
            """);
        }
    }
}
