using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class MatchesRegexProcessor : AttributeProcessorBase<MatchesRegexAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidateStringLikePropertyType(attribute, in context)
            || !TryGetConstructorArgumentValue(attribute, argumentIndex: 0, in context, out string? valueExpression))
        {
            return false.WithDefaults(out aspect);
        }
        if (context.ContainingType.GetMembers(valueExpression).FirstOrDefault(m => m.Kind is SymbolKind.Property) is not IPropertySymbol { Type: INamedTypeSymbol namedType } regexProperty
            || !namedType.GetFullMetadataName().Equals(typeof(Regex).FullName, StringComparison.Ordinal))
        {
            context.Report(ValidationGeneratorDiagnostics.MemberTypeMismatch,
                context.Property.Name,
                context.ContainingType.Name,
                nameof(MatchesRegexAttribute),
                valueExpression,
                nameof(Regex));
            return false.WithDefaults(out aspect);
        }
        // get pattern
        if (regexProperty.GetAttributes().FirstOrDefault(a => a.AttributeClass?.GetFullMetadataName(includeGlobalNamespacePrefix: true) == KnownTypes.GeneratedRegexAttribute) is not AttributeData
            {
                ConstructorArguments:
                [
                { Value: string pattern }, ..
                ]
            })
        {
            context.Report(ValidationGeneratorDiagnostics.PropertyAttributePreconditionNotMet,
                context.Property.Name,
                context.ContainingType.Name,
                nameof(MatchesRegexAttribute),
                $"The property '{valueExpression}' must be annotated with [GeneratedRegex] and specify the regex pattern to be used for validation.");
            return false.WithDefaults(out aspect);
        }
        aspect = new RegexValidationAspect(valueExpression, pattern);
        return true;
    }

    private sealed class RegexValidationAspect(string regexMember, string pattern) : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.NullAwareCondition($"!{regexMember}.IsMatch({model.StringContentExpression})")}})
            {
                errors.Add({{CreateValidationError(model, "match_regex", $"Property '{{nameof({model.AccessExpression})}}' must match the following pattern: '{{{SymbolDisplay.FormatLiteral(pattern, quote: true)}}}'.")}});
            }
            """);
        }
    }
}
