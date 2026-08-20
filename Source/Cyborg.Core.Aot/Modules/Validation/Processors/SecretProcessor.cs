using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class SecretProcessor : AttributeProcessorBase<SecretAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (context.Property.HasAttribute<UntaggedAttribute>())
        {
            context.Report(
                ValidationGeneratorDiagnostics.SecretAndUntaggedAreMutuallyExclusive,
                context.Property.Name,
                context.ContainingType.Name);
            return false.WithDefaults(out aspect);
        }
        if (!TypeSymbolHelpers.IsTaggedString(context.Property.Type, context.ContractInfo))
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
}

internal sealed class SecretAspect : PropertyAspect
{
    public override void RegisterDescriptorHints(
        List<string> hints,
        ValidationContractInfo contractInfo,
        DiagnosticsReporter diagnosticsReporter,
        PropertyModel property)
    {
        if (!hints.Contains(contractInfo.SecretTag))
        {
            hints.Add(contractInfo.SecretTag);
        }
    }

    public override string RewritePreparedValueExpression(PropertyRewriteContext context, string currentExpression)
    {
        string taggedStringType = context.ContractInfo.TaggedString.RenderGlobal();
        string tagExpression = context.ContractInfo.SecretTagExpression;
        if (context.Property.IsNullable)
        {
            return $"({currentExpression}) is {taggedStringType} secretValue ? secretValue.WithTag({tagExpression}) : null";
        }

        return $"({currentExpression}).WithTag({tagExpression})";
    }

    protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
    {
        string access = model.AccessExpression;
        string condition = model.RequiresNullGuard
            ? $"{access} is {{ }} secretValue && !secretValue.HasTag({model.ContractInfo.SecretTagExpression})"
            : $"!{access}.HasTag({model.ContractInfo.SecretTagExpression})";
        builder.AppendBlock(
        $$"""
        if ({{condition}})
        {
            errors.Add({{CreateValidationError(model, "secret", $"{model.TargetDescription} '{{{model.PropertyNameExpression}}}' must carry the '{model.ContractInfo.SecretTag}' tag.")}});
        }
        """);
    }
}
