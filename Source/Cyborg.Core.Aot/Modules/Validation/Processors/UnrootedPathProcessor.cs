using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class UnrootedPathProcessor : AttributeProcessorBase<UnrootedPathAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidateStringLikePropertyType(attribute, in context))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new UnrootedPathValidationAspect();
        return true;
    }

    private sealed class UnrootedPathValidationAspect : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.NullAwareCondition($"{KnownTypes.Path}.IsPathRooted({model.StringContentExpression})")}})
            {
                errors.Add({{CreateValidationError(model, rule: "unrooted_path", $"Property '{{nameof({model.AccessExpression})}}' must be an unrooted path, but was '{{{model.AccessExpression}}}'")}});
            }
            """);
        }
    }
}
