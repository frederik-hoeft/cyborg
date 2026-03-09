using System.Collections.Immutable;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DefaultValueAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(DefaultValueAttribute<>).FullName;

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
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name);
            return false;
        }

        if (attribute.ConstructorArguments.Length == 0)
        {
            context.Report(ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral, context.Property.Name, context.ContainingType.Name);
            return false;
        }

        if (!LiteralExpressionFactory.TryGetLiteralExpression(attribute.ConstructorArguments[0], context.Property.Type, out string? valueExpression))
        {
            context.Report(ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral, context.Property.Name, context.ContainingType.Name);
            return false;
        }

        ImmutableArray<string>.Builder whenPresentExpressions = ImmutableArray.CreateBuilder<string>();
        if (attribute.ConstructorArguments.Length > 1 && !attribute.ConstructorArguments[1].IsNull)
        {
            foreach (TypedConstant item in attribute.ConstructorArguments[1].Values)
            {
                if (!LiteralExpressionFactory.TryGetLiteralExpression(item, context.Property.Type, out string? itemExpression))
                {
                    context.Report(ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral, context.Property.Name, context.ContainingType.Name);
                    return false;
                }

                whenPresentExpressions.Add(itemExpression!);
            }
        }

        aspect = new DefaultValueValidationAspect(valueExpression!, whenPresentExpressions.ToImmutable());
        return true;
    }

    private sealed class DefaultValueValidationAspect(string valueExpression, ImmutableArray<string> whenPresentExpressions) : PropertyValidationAspect
    {
        public override string? RewriteDefaultAssignmentExpression(PropertyModel property, string moduleVariable, string? currentExpression)
        {
            string equalityComparer = LiteralExpressionFactory.GetDefaultEqualityComparer(property.TypeName);
            string triggerExpression;

            if (whenPresentExpressions.Length == 0)
            {
                triggerExpression = $"{equalityComparer}.Equals({currentExpression}, default!)";
            }
            else
            {
                List<string> checks = new(whenPresentExpressions.Length);
                foreach (string whenPresentExpression in whenPresentExpressions)
                {
                    checks.Add($"{equalityComparer}.Equals({currentExpression}, {whenPresentExpression})");
                }

                triggerExpression = string.Join(" || ", checks);
            }

            return $"{triggerExpression} ? {valueExpression} : {currentExpression}";
        }
    }
}
