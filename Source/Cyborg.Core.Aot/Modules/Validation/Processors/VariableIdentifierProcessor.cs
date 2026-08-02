using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class VariableIdentifierProcessor : PropertyValidationProcessorBase<VariableIdentifierAttribute>
{
    protected override bool TryProcessValidation(
        AttributeData attribute,
        ref readonly PropertyProcessingContext context,
        ref readonly PropertyValidationTarget target,
        out PropertyValidationAspect? aspect)
    {
        if (!ValidateTargetType(attribute, in context, in target, SpecialType.System_String))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new VariableIdentifierAspect();
        return true;
    }

    private sealed class VariableIdentifierAspect : PropertyValidationAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.AccessExpression}} is not null && !runtime.Environment.SyntaxFactory.IsValidIdentifier({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, rule: "valid_identifier", $"{model.TargetDescription} '{{{model.PropertyNameExpression}}}' must be a valid variable identifier, but was '{{{model.AccessExpression}}}'.")}});
            }
            """);
        }
    }
}
