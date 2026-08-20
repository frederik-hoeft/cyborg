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
        if (!TypeSymbolHelpers.IsTaggedString(context.Property.Type))
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
        DiagnosticsReporter diagnosticsReporter,
        PropertyModel property)
    {
        if (!hints.Contains(TypeSymbolHelpers.WellKnownSecretTag))
        {
            hints.Add(TypeSymbolHelpers.WellKnownSecretTag);
        }
    }

    public override string RewriteInterpolationExpression(PropertyRewriteContext context, string currentExpression) =>
        $"{currentExpression}.WithTag({TypeSymbolHelpers.WellKnownTagsSecretExpression})";

    protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
    {
        string access = model.AccessExpression;
        string condition = model.RequiresNullGuard
            ? $"{access} is {{ }} secretValue && !secretValue.HasTag({TypeSymbolHelpers.WellKnownTagsSecretExpression})"
            : $"!{access}.HasTag({TypeSymbolHelpers.WellKnownTagsSecretExpression})";
        builder.AppendBlock(
        $$"""
        if ({{condition}})
        {
            errors.Add({{CreateValidationError(model, "secret", $"{model.TargetDescription} '{{{model.PropertyNameExpression}}}' must carry the '{TypeSymbolHelpers.WellKnownSecretTag}' tag.")}});
        }
        """);
    }
}
