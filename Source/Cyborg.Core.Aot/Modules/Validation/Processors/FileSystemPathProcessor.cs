using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal abstract class FileSystemPathProcessor<TAttribute> : AttributeProcessorBase<TAttribute> where TAttribute : Attribute
{
    protected abstract string AttributeName { get; }

    protected abstract string ErrorCode { get; }

    protected abstract string PathKindDisplayName { get; }

    protected abstract string BuildExistsExpression();

    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidateStringLikePropertyType(attribute, in context))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new FilesystemPathValidationAspect(
            ErrorCode,
            PathKindDisplayName,
            BuildExistsExpression());
        return true;
    }

    private sealed class FilesystemPathValidationAspect(string errorCode, string pathKindDisplayName, string existsExpression) : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.NullAwareCondition($"!{existsExpression}({model.StringContentExpression})")}})
            {
                {{model.Variables.Errors}}.Add({{CreateValidationError(model, errorCode, $"Property '{{nameof({model.AccessExpression})}}' requires an existing {pathKindDisplayName} at '{{{model.DisplayExpression}}}'.")}});
            }
            """);
        }
    }
}
