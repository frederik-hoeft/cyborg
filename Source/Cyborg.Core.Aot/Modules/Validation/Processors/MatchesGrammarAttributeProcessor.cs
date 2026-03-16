using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class MatchesGrammarAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(MatchesGrammarAttribute).FullName;

    public bool TryProcess(PropertyProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        aspect = null;

        INamedTypeSymbol? attributeClass = attribute.AttributeClass;
        if (attributeClass is null)
        {
            return true;
        }
        if (context.Property.Type.SpecialType is not SpecialType.System_String)
        {
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name, nameof(MatchesGrammarAttribute), nameof(String));
            return false;
        }
        if (attribute.ConstructorArguments.Length == 0)
        {
            context.Report(ValidationGeneratorDiagnostics.MissingArgument, context.Property.Name, context.ContainingType.Name, nameof(MatchesGrammarAttribute));
            return false;
        }
        if (attribute.ConstructorArguments[0].Value is not string valueExpression)
        {
            context.Report(ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral, context.Property.Name, context.ContainingType.Name);
            return false;
        }
        if (context.ContainingType.GetMembers(valueExpression).FirstOrDefault(m => m.Kind is SymbolKind.Property) is not IPropertySymbol { Type: INamedTypeSymbol namedType } parserProperty)
        {
            context.Report(ValidationGeneratorDiagnostics.MemberNotFound, 
                context.Property.Name, 
                context.ContainingType.Name, 
                nameof(MatchesGrammarAttribute),
                valueExpression);
            return false;
        }
        aspect = new GrammarValidationAspect(parserProperty, valueExpression);
        return true;
    }

    private sealed class GrammarValidationAspect(IPropertySymbol parserProperty, string valueExpression) : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
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
            if ({{model.AccessExpression}} is not null && !{{valueExpression}}.TryParse({{model.AccessExpression}}, out _, out _))
            {
                errors.Add({{CreateValidationError(model, "match_grammar", $"Property '{{nameof({model.AccessExpression})}}' does not match the required grammar.")}});
            }
            """);
        }
    }
}
