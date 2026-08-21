using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class SecretProcessor : AttributeProcessorBase<SecretAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out IPropertyAspect? aspect)
    {
        if (context.Property.HasAttribute<UntaggedAttribute>())
        {
            context.Report(
                ValidationGeneratorDiagnostics.SecretAndUntaggedAreMutuallyExclusive,
                context.Property.Name,
                context.ContainingType.Name);
            return false.WithDefaults(out aspect);
        }
        if (!context.Property.Type.EqualsIgnoreNullability(context.ContractInfo.TaggedString))
        {
            context.Report(
                ValidationGeneratorDiagnostics.SecretRequiresTaggedString,
                context.Property.Name,
                context.ContainingType.Name,
                context.Property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            return false.WithDefaults(out aspect);
        }
        aspect = new SecretAspect();
        return true;
    }

    private sealed class SecretAspect : PropertyValidationAspect, IPropertyDescriptionAspect, IPropertyPreparationAspect
    {
        public void RegisterDescriptorHints(List<string> hints, ValidationContractInfo contractInfo, DiagnosticsReporter diagnosticsReporter, PropertyModel property)
        {
            if (!hints.Contains(contractInfo.SecretTag))
            {
                hints.Add(contractInfo.SecretTag);
            }
        }

        public string RewritePreparedValueExpression(PropertyRewriteContext context, string currentExpression)
        {
            string taggedStringType = context.ContractInfo.TaggedString.RenderGlobal();
            string tagExpression = context.ContractInfo.SecretTagExpression;
            if (context.Property.IsNullable)
            {
                return $"({currentExpression}) is {taggedStringType} secretValue ? secretValue.WithTag({tagExpression}) : null";
            }

            return $"({currentExpression}).WithTag({tagExpression})";
        }

        public override void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model)
        {
            string access = model.AccessExpression;
            string condition = model.TargetType.CanEverBeNull
                ? $"{access} is {{ }} secretValue && !secretValue.HasTag({model.ContractInfo.SecretTagExpression})"
                : $"!{access}.HasTag({model.ContractInfo.SecretTagExpression})";
            builder.AppendBlock(
            $$"""
            if ({{condition}})
            {
                {{model.Variables.Errors}}.Add({{CreateValidationError(model, "secret", $"{model.TargetDescription} '{{{model.PropertyNameExpression}}}' must carry the '{model.ContractInfo.SecretTag}' tag.")}});
            }
            """);
        }
    }
}

