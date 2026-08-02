using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class FileNameProcessor : AttributeProcessorBase<FileNameAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidatePropertyType(attribute, in context, SpecialType.System_String))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new FileNameValidationAspect();
        return true;
    }

    private sealed class FileNameValidationAspect : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.AccessExpression}} is not null && !{{KnownTypes.ValidationRuntimeHelpers}}.IsValidFileName({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, rule: "file_name", $"Property '{{nameof({model.AccessExpression})}}' must be a valid file name, but was '{{{model.AccessExpression}}}'")}});
            }
            """);
        }
    }
}
