using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class RangeAttributeProcessor : AttributeProcessorBase
{
    public override string AttributeMetadataName => typeof(RangeAttribute<>).FullName;

    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidateTypeArguments(attribute, in context, context.Property.Type)
            || !TryGetNamedArgumentExpressions(attribute, in context, out Dictionary<string, string?> namedArgumentExpressions))
        {
            return false.WithDefaults(out aspect);
        }
        if (namedArgumentExpressions.Count == 0)
        {
            context.Report(ValidationGeneratorDiagnostics.MissingArgument, context.Property.Name, context.ContainingType.Name, nameof(RangeAttribute<>));
            return false.WithDefaults(out aspect);
        }
        aspect = new RangeValidationAspect
        (
            namedArgumentExpressions.GetValueOrDefault(nameof(RangeAttribute<>.Min)),
            namedArgumentExpressions.GetValueOrDefault(nameof(RangeAttribute<>.Max))
        );
        return true;
    }

    private sealed class RangeValidationAspect(string? minExpression, string? maxExpression) : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            if (minExpression is not null)
            {
                builder.AppendBlock(
                $$"""
                if ({{model.AccessExpression}} < {{minExpression}})
                {
                    errors.Add({{CreateValidationError(model, "range", $"Property '{{nameof({model.AccessExpression})}}' must not be greater than the configured minimum '{minExpression}', was '{{{model.AccessExpression}}}'.")}});
                }
                """);
            }

            if (maxExpression is not null)
            {
                builder.AppendBlock(
                $$"""
                if ({{model.AccessExpression}} > {{maxExpression}})
                {
                    errors.Add({{CreateValidationError(model, "range", $"Property '{{nameof({model.AccessExpression})}}' must not be greater than the configured maximum '{maxExpression}', was '{{{model.AccessExpression}}}'.")}});
                }
                """);
            }
        }
    }
}
