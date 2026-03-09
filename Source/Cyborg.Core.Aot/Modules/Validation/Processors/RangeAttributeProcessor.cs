using System.Text;
using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name, nameof(RangeAttribute<>));
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
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel property)
        {
            if (minExpression is not null)
            {
                builder.AppendBlock(
                $$"""
                if ({{property.AccessExpression}} < {{minExpression}})
                {
                    errors.Add({{CreateValidationError(property, "range", $"Property '{{nameof({property.AccessExpression})}}' must be greater than or equal to the configured minimum.")}});
                }
                """);
            }

            if (maxExpression is not null)
            {
                builder.AppendBlock(
                $$"""
                if ({{property.AccessExpression}} > {{maxExpression}})
                {
                    errors.Add({{CreateValidationError(property, "range", $"Property '{{nameof({property.AccessExpression})}}' must be less than or equal to the configured maximum.")}});
                }
                """);
            }
        }
    }
}
