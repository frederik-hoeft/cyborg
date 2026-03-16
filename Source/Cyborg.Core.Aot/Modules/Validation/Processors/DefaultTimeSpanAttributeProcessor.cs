using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Globalization;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DefaultTimeSpanAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(DefaultTimeSpanAttribute).FullName;

    public bool TryProcess(PropertyProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
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

        if (attribute.ConstructorArguments is not [{ Value: string literalValue }])
        {
            context.Report(ValidationGeneratorDiagnostics.MissingArgument, context.Property.Name, context.ContainingType.Name, nameof(DefaultTimeSpanAttribute));
            return false;
        }
        if (!TimeSpan.TryParseExact(literalValue, "c", CultureInfo.InvariantCulture, out _))
        {
            context.Report(ValidationGeneratorDiagnostics.InvalidTimeSpanLiteral, context.Property.Name, context.ContainingType.Name, nameof(DefaultTimeSpanAttribute), literalValue);
            return false;
        }

        string? valueExpression = SymbolDisplay.FormatLiteral(literalValue, quote: true);

        aspect = new DefaultValueValidationAspect(valueExpression);
        return true;
    }

    private sealed class DefaultValueValidationAspect(string valueExpression) : PropertyValidationAspect
    {
        public override bool EnsuresDefault => true;

        public override string? RewriteDefaultAssignmentExpression(PropertyRewriteContext context, string? currentExpression)
        {
            string propertyAccessExpression = context.PropertyAccessExpression;
            string equalityComparer = KnownTypes.DefaultEqualityComparerOfT(context.Property.NullableTypeName);
            string triggerExpression = $"{equalityComparer}.Equals({propertyAccessExpression}, default!)";
            return $$"""
                {{triggerExpression}} ? {{KnownTypes.TimeSpan}}.{{nameof(TimeSpan.ParseExact)}}({{valueExpression}}, "c", {{KnownTypes.InvariantCulture}}) : {{propertyAccessExpression}}
                """;
        }
    }
}
