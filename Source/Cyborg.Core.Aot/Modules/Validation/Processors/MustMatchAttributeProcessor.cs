using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class MustMatchAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(MustMatchAttribute).FullName;

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
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name, nameof(MustMatchAttribute), nameof(String));
            return false;
        }
        if (attribute.ConstructorArguments.Length == 0)
        {
            context.Report(ValidationGeneratorDiagnostics.MissingArgument, context.Property.Name, context.ContainingType.Name, nameof(MustMatchAttribute));
            return false;
        }
        if (attribute.ConstructorArguments[0].Value is not string valueExpression)
        {
            context.Report(ValidationGeneratorDiagnostics.UnsupportedAttributeLiteral, context.Property.Name, context.ContainingType.Name);
            return false;
        }
        if (context.ContainingType.GetMembers(valueExpression).FirstOrDefault(m => m.Kind is SymbolKind.Property) is not IPropertySymbol { Type: INamedTypeSymbol namedType } regexProperty
            || !namedType.GetFullMetadataName().Equals(typeof(Regex).FullName, StringComparison.Ordinal))
        {
            context.Report(ValidationGeneratorDiagnostics.MemberTypeMismatch, 
                context.Property.Name, 
                context.ContainingType.Name, 
                nameof(MustMatchAttribute),
                valueExpression,
                nameof(Regex));
            return false;
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
                nameof(MustMatchAttribute),
                $"The property '{valueExpression}' must be annotated with [GeneratedRegex] and specify the regex pattern to be used for validation.");
            return false;
        }
        aspect = new RegexValidationAspect(valueExpression, pattern);
        return true;
    }

    private sealed class RegexValidationAspect(string regexMember, string pattern) : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.AccessExpression}} is not null && !{{regexMember}}.IsMatch({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, "must_match", $"Property '{{nameof({model.AccessExpression})}}' must match the following pattern: '{{{SymbolDisplay.FormatLiteral(pattern, quote: true)}}}'.")}});
            }
            """);
        }
    }
}
