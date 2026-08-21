using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class MatchesGrammarProcessor : AttributeProcessorBase<MatchesGrammarAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out IPropertyAspect? aspect)
    {
        if (!ValidateStringLikePropertyType(attribute, in context)
            || !TryGetConstructorArgumentValue(attribute, argumentIndex: 0, in context, out string? valueExpression))
        {
            return false.WithDefaults(out aspect);
        }
        if (context.ContainingType.GetMembers(valueExpression).FirstOrDefault(m => m.Kind is SymbolKind.Property) is not IPropertySymbol { Type: INamedTypeSymbol namedType } parserProperty)
        {
            context.Report(ValidationGeneratorDiagnostics.MemberNotFound,
                context.Property.Name,
                context.ContainingType.Name,
                nameof(MatchesGrammarAttribute),
                valueExpression);
            return false.WithDefaults(out aspect);
        }
        aspect = new GrammarValidationAspect(parserProperty, valueExpression);
        return true;
    }

    private sealed class GrammarValidationAspect(IPropertySymbol parserProperty, string valueExpression) : PropertyValidationAspect
    {
        public override void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model)
        {
            if (!SymbolEqualityComparer.Default.Equals(parserProperty.Type, model.ContractInfo.IParser))
            {
                model.DiagnosticsReporter.Report(ValidationGeneratorDiagnostics.MemberTypeMismatch,
                    model.Property.Symbol.Locations.FirstOrDefault() ?? Location.None,
                    model.Property.Name,
                    parserProperty.ContainingType.Name,
                    nameof(MatchesGrammarAttribute),
                    valueExpression,
                    model.ContractInfo.IParser.Name);
                return;
            }
            // bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed);
            builder.AppendBlock(
            $$"""
            if ({{model.NullAwareCondition($"!{valueExpression}.TryParse({model.StringContentExpression}, out _, out _)")}})
            {
                {{model.Variables.Errors}}.Add({{CreateValidationError(model, "match_grammar", $"Property '{{nameof({model.AccessExpression})}}' does not match the required grammar.")}});
            }
            """);
        }
    }
}
