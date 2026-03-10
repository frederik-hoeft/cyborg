using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DefaultTimeSpanAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(DefaultTimeSpanAttribute).FullName;

    public bool TryProcess(PropertyAttributeProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        aspect = null;

        INamedTypeSymbol? attributeClass = attribute.AttributeClass;
        if (attributeClass is null)
        {
            return true;
        }

        // ensure property is of type TimeSpan
        string actual = context.Property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!actual.Equals(KnownTypes.TimeSpan))
        {
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name, nameof(DefaultTimeSpanAttribute), nameof(TimeSpan));
            return false;
        }

        if (attribute.ConstructorArguments.Length == 0)
        {
            context.Report(ValidationGeneratorDiagnostics.MissingArgument, context.Property.Name, context.ContainingType.Name, nameof(DefaultTimeSpanAttribute));
            return false;
        }

        string? valueExpression = SymbolDisplay.FormatLiteral((string)attribute.ConstructorArguments[0].Value!, quote: true);

        aspect = new DefaultValueValidationAspect(valueExpression);
        return true;
    }

    private sealed class DefaultValueValidationAspect(string valueExpression) : PropertyValidationAspect
    {
        public override string? RewriteDefaultAssignmentExpression(PropertyModel property, string moduleVariable, string propertyAccessExpression, string? currentExpression)
        {
            string equalityComparer = LiteralExpressionFactory.GetDefaultEqualityComparer(property.NullableTypeName);
            string triggerExpression = $"{equalityComparer}.Equals({propertyAccessExpression}, default!)";
            return $"{triggerExpression} ? {KnownTypes.TimeSpan}.{nameof(TimeSpan.Parse)}({valueExpression}) : {propertyAccessExpression}";
        }
    }
}
