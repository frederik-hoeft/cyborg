using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;
using System.Globalization;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal abstract class LengthAttributeProcessorBase<TAttribute> : PropertyValidationProcessorBase<TAttribute> where TAttribute : PropertyValidationAttribute
{
    protected override bool TryProcessValidation(AttributeData attribute, ref readonly PropertyProcessingContext context, ref readonly PropertyValidationTarget target, out PropertyValidationAspect? aspect)
    {
        aspect = null;

        LengthTargetKind targetKind = GetTargetKind(target.Type, context.ContractInfo, out INamedTypeSymbol? collectionInterface);
        if (targetKind == LengthTargetKind.None)
        {
            context.Report(
                ValidationGeneratorDiagnostics.UnsupportedLengthTargetType,
                context.Property.Name,
                context.ContainingType.Name,
                target.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            return false;
        }

        if (!TryGetBounds(attribute, in context, out int? min, out int? max))
        {
            return false;
        }

        if (min is < 0)
        {
            context.Report(ValidationGeneratorDiagnostics.LengthArgumentMustBeNonNegative,
                context.Property.Name,
                context.ContainingType.Name,
                "Min",
                min.Value.ToString(CultureInfo.InvariantCulture));

            return false;
        }

        if (max is < 0)
        {
            context.Report(ValidationGeneratorDiagnostics.LengthArgumentMustBeNonNegative,
                context.Property.Name,
                context.ContainingType.Name,
                "Max",
                max.Value.ToString(CultureInfo.InvariantCulture));

            return false;
        }

        if (min is not null && max is not null && min > max)
        {
            context.Report(ValidationGeneratorDiagnostics.InvalidRangeBounds,
                context.Property.Name,
                context.ContainingType.Name,
                min.Value.ToString(CultureInfo.InvariantCulture),
                max.Value.ToString(CultureInfo.InvariantCulture));

            return false;
        }

        aspect = new LengthValidationAspect(
            targetKind,
            collectionInterface,
            minExpression: min?.ToString(CultureInfo.InvariantCulture),
            maxExpression: max?.ToString(CultureInfo.InvariantCulture),
            requiresNullGuard: RequiresNullGuard(target.Type));

        return true;
    }

    protected abstract bool TryGetBounds(AttributeData attribute, ref readonly PropertyProcessingContext context, out int? min, out int? max);

    private static bool RequiresNullGuard(ITypeSymbol propertyType) =>
        propertyType.IsReferenceType || propertyType.NullableAnnotation == NullableAnnotation.Annotated;

    private static LengthTargetKind GetTargetKind(ITypeSymbol propertyType, ValidationContractInfo contractInfo, out INamedTypeSymbol? collectionInterface)
    {
        collectionInterface = null;
        if (propertyType.SpecialType == SpecialType.System_String || TypeSymbolHelpers.IsTaggedString(propertyType, contractInfo))
        {
            return LengthTargetKind.String;
        }

        if (ImplementsIReadOnlyCollection(propertyType, out collectionInterface))
        {
            return LengthTargetKind.Collection;
        }

        return LengthTargetKind.None;
    }

    private static bool ImplementsIReadOnlyCollection(ITypeSymbol type, out INamedTypeSymbol? collectionInterface)
    {
        collectionInterface = null;
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (IsReadOnlyCollection(namedType))
        {
            return true;
        }

        foreach (INamedTypeSymbol iface in namedType.AllInterfaces)
        {
            if (IsReadOnlyCollection(iface))
            {
                collectionInterface = iface;
                return true;
            }
        }

        return false;
    }

    private static bool IsReadOnlyCollection(INamedTypeSymbol type) =>
        type.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_IReadOnlyCollection_T;

    private enum LengthTargetKind
    {
        None = 0,
        String,
        Collection
    }

    private sealed class LengthValidationAspect
    (
        LengthTargetKind targetKind,
        INamedTypeSymbol? collectionInterface,
        string? minExpression,
        string? maxExpression,
        bool requiresNullGuard
    ) : PropertyValidationAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            if (requiresNullGuard)
            {
                builder.AppendLine($"if ({model.AccessExpression} is not null)");
                builder.AppendLine("{");
                EmitLengthValidation(builder.IncreaseIndent(), model);
                builder.AppendLine("}");
                return;
            }

            EmitLengthValidation(builder, model);
        }

        private void EmitLengthValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            string accessExpression;
            if (collectionInterface is null)
            {
                accessExpression = model.AccessExpression;
            }
            else
            {
                accessExpression = $"{model.AccessExpression.Replace('.', '_')}__collection";
                builder.AppendLine($"{collectionInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included))} {accessExpression} = {model.AccessExpression};");
            }
            string sizeExpression = targetKind switch
            {
                LengthTargetKind.String => $"{model.StringContentExpression}.Length",
                LengthTargetKind.Collection => $"{accessExpression}.Count",
                _ => throw new InvalidOperationException("Unsupported length target kind.")
            };

            if (minExpression is not null)
            {
                builder.AppendBlock(
                $$"""
                if ({{sizeExpression}} < {{minExpression}})
                {
                    errors.Add({{CreateValidationError(model, "length", $"{model.TargetDescription} '{{{model.PropertyNameExpression}}}' must have a length/count not smaller than configured minimum '{minExpression}', was '{{{sizeExpression}}}'.")}});
                }
                """);
            }

            if (maxExpression is not null)
            {
                builder.AppendBlock(
                $$"""
                if ({{sizeExpression}} > {{maxExpression}})
                {
                    errors.Add({{CreateValidationError(model, "length", $"{model.TargetDescription} '{{{model.PropertyNameExpression}}}' must have a length/count not greater than configured maximum '{maxExpression}', was '{{{sizeExpression}}}'.")}});
                }
                """);
            }
        }
    }
}
