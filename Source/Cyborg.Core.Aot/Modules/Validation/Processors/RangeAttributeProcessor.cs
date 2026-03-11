using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Model;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class RangeAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(RangeAttribute<>).FullName;

    public bool TryProcess(PropertyAttributeProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        aspect = null;

        INamedTypeSymbol? attributeClass = attribute.AttributeClass;
        if (attributeClass is null)
        {
            return true;
        }
        
        if (!SymbolEqualityComparer.Default.Equals(context.Property.Type, attributeClass.TypeArguments[0]))
        {
            context.Report(ValidationGeneratorDiagnostics.GenericTypeMismatch, context.Property.Name, context.ContainingType.Name, nameof(RangeAttribute<>));
            return false;
        }

        Dictionary<string, string?> namedArgumentExpressions = new(capacity: 2);
        foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
        {
            if (!named.Value.IsNull)
            {
                if (!LiteralExpressionFactory.TryGetLiteralExpression(named.Value, context.Property.Type, out string? expression))
                {
                    context.Report(
                        ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral,
                        context.Property.Name,
                        context.ContainingType.Name);
                    return false;
                }
                namedArgumentExpressions[named.Key] = expression;
            }
        }

        if (namedArgumentExpressions.Count == 0)
        {
            context.Report(ValidationGeneratorDiagnostics.MissingArgument, context.Property.Name, context.ContainingType.Name, nameof(RangeAttribute<>));
            return false;
        }

        aspect = new RangeValidationAspect
        (
            namedArgumentExpressions.GetValueOrDefault(nameof(RangeAttribute<>.Min)),
            namedArgumentExpressions.GetValueOrDefault(nameof(RangeAttribute<>.Max))
        );
        return true;
    }

    private sealed class RangeValidationAspect(string? minExpression, string? maxExpression) : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

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