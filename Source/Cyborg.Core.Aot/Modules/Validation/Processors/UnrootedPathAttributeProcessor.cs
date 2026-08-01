using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class UnrootedPathAttributeProcessor : AttributeProcessorBase<UnrootedPathAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidatePropertyType(attribute, in context, SpecialType.System_String))
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
            if ({{model.AccessExpression}} is not null && {{KnownTypes.Path}}.IsPathRooted({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, rule: "unrooted_path", $"Property '{{nameof({model.AccessExpression})}}' must be an unrooted path, but was '{{{model.AccessExpression}}}'")}});
            }
            """);
        }
    }
}
