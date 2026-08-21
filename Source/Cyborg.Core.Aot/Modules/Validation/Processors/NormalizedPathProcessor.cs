using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class NormalizedPathProcessor : AttributeProcessorBase<NormalizedPathAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidateStringLikePropertyType(attribute, in context))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new NormalizedPathValidationAspect();
        return true;
    }

    private sealed class NormalizedPathValidationAspect : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.NullAwareCondition($"!{KnownTypes.ValidationRuntimeHelpers}.IsNormalizedPath({model.StringContentExpression})")}})
            {
                {{model.Variables.Errors}}.Add({{CreateValidationError(model, rule: "normalized_path", $"Property '{{nameof({model.AccessExpression})}}' must be a normalized path, but was '{{{model.DisplayExpression}}}'")}});
            }
            """);
        }
    }
}
