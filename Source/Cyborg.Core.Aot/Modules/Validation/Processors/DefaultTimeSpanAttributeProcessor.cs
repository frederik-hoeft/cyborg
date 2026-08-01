using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Globalization;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DefaultTimeSpanAttributeProcessor : AttributeProcessorBase<DefaultTimeSpanAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        // ensure property is of type TimeSpan
        string actual = context.Property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!actual.Equals(KnownTypes.TimeSpan))
        {
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name, nameof(DefaultTimeSpanAttribute), nameof(TimeSpan));
            return false.WithDefaults(out aspect);
        }

        if (!TryGetConstructorArgumentValue(attribute, argumentIndex: 0, in context, out string? literalValue))
        {
            return false.WithDefaults(out aspect);
        }
        if (!TimeSpan.TryParseExact(literalValue, "c", CultureInfo.InvariantCulture, out _))
        {
            context.Report(ValidationGeneratorDiagnostics.InvalidTimeSpanLiteral, context.Property.Name, context.ContainingType.Name, nameof(DefaultTimeSpanAttribute), literalValue);
            return false.WithDefaults(out aspect);
        }

        string? valueExpression = SymbolDisplay.FormatLiteral(literalValue, quote: true);

        aspect = new DefaultValueValidationAspect(valueExpression);
        return true;
    }

    private sealed class DefaultValueValidationAspect(string valueExpression) : PropertyAspect(ensuresDefault: true)
    {
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
