using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class FileNameProcessor : AttributeProcessorBase<FileNameAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out IPropertyAspect? aspect)
    {
        if (!ValidateStringLikePropertyType(attribute, in context))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new FileNameValidationAspect();
        return true;
    }

    private sealed class FileNameValidationAspect : PropertyValidationAspect
    {
        public override void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.NullAwareCondition($"!{KnownTypes.ValidationRuntimeHelpers}.IsValidFileName({model.StringContentExpression})")}})
            {
                {{model.Variables.Errors}}.Add({{CreateValidationError(model, rule: "file_name", $"{model.TargetDescription} must be a valid file name, but was '{{{model.DisplayExpression}}}'")}});
            }
            """);
        }
    }
}
