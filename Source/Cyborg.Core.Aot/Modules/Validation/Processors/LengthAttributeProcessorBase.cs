using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;
using Cyborg.Shared.Text;
using Microsoft.CodeAnalysis;
using System.Globalization;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal abstract class LengthAttributeProcessorBase<TAttribute> : PropertyValidationProcessorBase<TAttribute> where TAttribute : PropertyValidationAttribute
{
    protected override bool TryProcessValidation(AttributeData attribute, ref readonly PropertyProcessingContext context, ref readonly PropertyValidationTarget target, out PropertyValidationAspect? aspect)
    {
        aspect = null;

        LengthTargetKind targetKind = GetTargetKind(target.Type, in context, out CollectionShape? collectionShape);
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
            collectionShape,
            minExpression: min?.ToString(CultureInfo.InvariantCulture),
            maxExpression: max?.ToString(CultureInfo.InvariantCulture));

        return true;
    }

    protected abstract bool TryGetBounds(AttributeData attribute, ref readonly PropertyProcessingContext context, out int? min, out int? max);

    private static LengthTargetKind GetTargetKind(ITypeSymbol targetType, ref readonly PropertyProcessingContext context, out CollectionShape? collectionShape)
    {
        collectionShape = null;
        if (targetType.IsStringLike(context.ContractInfo.TaggedString))
        {
            return LengthTargetKind.String;
        }

        if (CollectionTypeInspector.TryDescribe(context.Compilation, targetType, out collectionShape) && collectionShape.SupportsCount)
        {
            return LengthTargetKind.Collection;
        }

        return LengthTargetKind.None;
    }

    private enum LengthTargetKind
    {
        None = 0,
        String,
        Collection
    }

    private sealed class LengthValidationAspect
    (
        LengthTargetKind targetKind,
        CollectionShape? collectionShape,
        string? minExpression,
        string? maxExpression
    ) : PropertyValidationAspect
    {
        public override void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model)
        {
            if (targetKind == LengthTargetKind.Collection)
            {
                CollectionShape shape = collectionShape
                    ?? throw new InvalidOperationException("Collection length validation is missing collection shape metadata.");
                ValueAccess access = shape.Renderer.Access(model.AccessExpression);
                if (access.RequiresGuard)
                {
                    builder.AppendBlock(
                        $$"""
                        if ({{access.GuardExpression}})
                        {
                        """);
                    EmitLengthValidation(builder.IncreaseIndent(), model, shape.Renderer.CountExpression(access.ValueExpression));
                    builder.AppendLine("}");
                    return;
                }

                EmitLengthValidation(builder, model, shape.Renderer.CountExpression(access.ValueExpression));
                return;
            }

            if (model.TargetType.CanEverBeNull)
            {
                builder.AppendBlock(
                    $$"""
                    if ({{model.AccessExpression}} is not null)
                    {
                    """);
                EmitLengthValidation(builder.IncreaseIndent(), model, CreateStringLengthExpression(model));
                builder.AppendLine("}");
                return;
            }

            EmitLengthValidation(builder, model, CreateStringLengthExpression(model));
        }

        private static string CreateStringLengthExpression(PropertyValidationModel model)
        {
            if (!model.IsTaggedString)
            {
                return $"{model.AccessExpression}.Length";
            }

            string contentExpression = model.TargetType is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
            }
                ? $"{model.AccessExpression}.Value.Value"
                : $"{model.AccessExpression}.Value";
            return $"{contentExpression}.Length";
        }

        private void EmitLengthValidation(IndentedStringBuilder builder, PropertyValidationModel model, string sizeExpression)
        {
            if (minExpression is not null)
            {
                builder.AppendBlock(
                $$"""
                if ({{sizeExpression}} < {{minExpression}})
                {
                    {{model.Variables.Errors}}.Add({{CreateValidationError(model, "length", $"{model.TargetDescription} must have a length/count not smaller than configured minimum '{minExpression}', was '{{{sizeExpression}}}'.")}});
                }
                """);
            }

            if (maxExpression is not null)
            {
                builder.AppendBlock(
                $$"""
                if ({{sizeExpression}} > {{maxExpression}})
                {
                    {{model.Variables.Errors}}.Add({{CreateValidationError(model, "length", $"{model.TargetDescription} must have a length/count not greater than configured maximum '{maxExpression}', was '{{{sizeExpression}}}'.")}});
                }
                """);
            }
        }
    }
}
