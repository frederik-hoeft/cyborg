using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class NormalizedPathProcessor : AttributeProcessorBase<NormalizedPathAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidatePropertyType(attribute, in context, SpecialType.System_String))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new NormalizedPathValidationAspect();
        return true;
    }

    private sealed class NormalizedPathValidationAspect : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.AccessExpression}} is not null && !{{KnownTypes.ValidationRuntimeHelpers}}.IsNormalizedPath({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, rule: "normalized_path", $"Property '{{nameof({model.AccessExpression})}}' must be a normalized path, but was '{{{model.AccessExpression}}}'")}});
            }
            """);
        }
    }
}
