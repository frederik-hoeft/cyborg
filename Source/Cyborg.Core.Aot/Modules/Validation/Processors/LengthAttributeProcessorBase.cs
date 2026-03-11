using System.Globalization;
using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal abstract class LengthAttributeProcessorBase : IPropertyAttributeProcessor
{
    public abstract string AttributeMetadataName { get; }

    public bool TryProcess(PropertyAttributeProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        aspect = null;

        LengthTargetKind targetKind = GetTargetKind(context.Property.Type, out INamedTypeSymbol? collectionInterface);
        if (targetKind == LengthTargetKind.None)
        {
            context.Report(
                ValidationGeneratorDiagnostics.UnsupportedLengthTargetType,
                context.Property.Name,
                context.ContainingType.Name,
                context.Property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            return false;
        }

        if (!TryGetBounds(context, attribute, out int? min, out int? max))
        {
            return false;
        }

        if (min is < 0)
        {
            context.Report(
                ValidationGeneratorDiagnostics.LengthArgumentMustBeNonNegative,
                context.Property.Name,
                context.ContainingType.Name,
                "Min",
                min.Value.ToString(CultureInfo.InvariantCulture));

            return false;
        }

        if (max is < 0)
        {
            context.Report(
                ValidationGeneratorDiagnostics.LengthArgumentMustBeNonNegative,
                context.Property.Name,
                context.ContainingType.Name,
                "Max",
                max.Value.ToString(CultureInfo.InvariantCulture));

            return false;
        }

        if (min is not null && max is not null && min > max)
        {
            context.Report(
                ValidationGeneratorDiagnostics.InvalidRangeBounds,
                context.Property.Name,
                context.ContainingType.Name,
                min.Value.ToString(CultureInfo.InvariantCulture),
                max.Value.ToString(CultureInfo.InvariantCulture));

            return false;
        }

        aspect = new LengthValidationAspect(
            targetKind,
            collectionInterface,
            min?.ToString(CultureInfo.InvariantCulture),
            max?.ToString(CultureInfo.InvariantCulture),
            requiresNullGuard: RequiresNullGuard(context.Property.Type));

        return true;
    }

    protected abstract bool TryGetBounds(
        PropertyAttributeProcessingContext context,
        AttributeData attribute,
        out int? min,
        out int? max);

    protected static bool TryGetSingleIntConstructorArgument(
        PropertyAttributeProcessingContext context,
        AttributeData attribute,
        string attributeDisplayName,
        out int value)
    {
        value = default;

        if (attribute.ConstructorArguments.Length != 1)
        {
            context.Report(
                ValidationGeneratorDiagnostics.MissingArgument,
                context.Property.Name,
                context.ContainingType.Name,
                attributeDisplayName);

            return false;
        }

        TypedConstant constant = attribute.ConstructorArguments[0];
        if (constant.IsNull || constant.Value is not int intValue)
        {
            context.Report(
                ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral,
                context.Property.Name,
                context.ContainingType.Name);

            return false;
        }

        value = intValue;
        return true;
    }

    protected static bool TryGetTwoIntConstructorArguments(
        PropertyAttributeProcessingContext context,
        AttributeData attribute,
        string attributeDisplayName,
        out int min,
        out int max)
    {
        min = default;
        max = default;

        if (attribute.ConstructorArguments.Length != 2)
        {
            context.Report(
                ValidationGeneratorDiagnostics.MissingArgument,
                context.Property.Name,
                context.ContainingType.Name,
                attributeDisplayName);

            return false;
        }

        TypedConstant minConstant = attribute.ConstructorArguments[0];
        TypedConstant maxConstant = attribute.ConstructorArguments[1];

        if (minConstant.IsNull || minConstant.Value is not int minValue
            || maxConstant.IsNull || maxConstant.Value is not int maxValue)
        {
            context.Report(
                ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral,
                context.Property.Name,
                context.ContainingType.Name);

            return false;
        }

        min = minValue;
        max = maxValue;
        return true;
    }

    private static bool RequiresNullGuard(ITypeSymbol propertyType) =>
        propertyType.IsReferenceType || propertyType.NullableAnnotation == NullableAnnotation.Annotated;

    private static LengthTargetKind GetTargetKind(ITypeSymbol propertyType, out INamedTypeSymbol? collectionInterface)
    {
        collectionInterface = null;
        if (propertyType.SpecialType == SpecialType.System_String)
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
        type.GetFullMetadataName(includeGlobalNamespacePrefix: true).Equals(KnownTypes.IReadOnlyCollectionT, StringComparison.Ordinal);

    private enum LengthTargetKind
    {
        None = 0,
        String,
        Collection
    }

    private sealed class LengthValidationAspect(
        LengthTargetKind targetKind,
        INamedTypeSymbol? collectionInterface,
        string? minExpression,
        string? maxExpression,
        bool requiresNullGuard) : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
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
                LengthTargetKind.String => $"{accessExpression}.Length",
                LengthTargetKind.Collection => $"{accessExpression}.Count",
                _ => throw new InvalidOperationException("Unsupported length target kind.")
            };

            if (requiresNullGuard)
            {
                builder.AppendLine($"if ({model.AccessExpression} is not null)");
                builder.AppendLine("{");
                builder = builder.IncreaseIndent();
            }

            if (minExpression is not null)
            {
                builder.AppendBlock(
                $$"""
                if ({{sizeExpression}} < {{minExpression}})
                {
                    errors.Add({{CreateValidationError(model, "length", $"Property '{{nameof({model.AccessExpression})}}' must have a length/count not smaller than configured minimum '{minExpression}', was '{{{sizeExpression}}}'.")}});
                }
                """);
            }

            if (maxExpression is not null)
            {
                builder.AppendBlock(
                $$"""
                if ({{sizeExpression}} > {{maxExpression}})
                {
                    errors.Add({{CreateValidationError(model, "length", $"Property '{{nameof({model.AccessExpression})}}' must have a length/count not greater than configured maximum '{maxExpression}', was '{{{sizeExpression}}}'.")}});
                }
                """);
            }

            if (requiresNullGuard)
            {
                builder = builder.DecreaseIndent();
                builder.AppendLine("}");
            }
        }
    }
}
