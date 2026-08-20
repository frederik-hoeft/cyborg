using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class RootedPathProcessor : AttributeProcessorBase<RootedPathAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidateStringLikePropertyType(attribute, in context))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new RootedPathValidationAspect();
        return true;
    }

    private sealed class RootedPathValidationAspect : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.NullAwareCondition($"!{KnownTypes.Path}.IsPathRooted({model.StringContentExpression})")}})
            {
                errors.Add({{CreateValidationError(model, rule: "rooted_path", $"Property '{{nameof({model.AccessExpression})}}' must be a rooted path, but was '{{{model.AccessExpression}}}'")}});
            }
            """);
        }
    }
}
