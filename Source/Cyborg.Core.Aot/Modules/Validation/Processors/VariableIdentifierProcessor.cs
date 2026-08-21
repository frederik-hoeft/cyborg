using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class VariableIdentifierProcessor : PropertyValidationProcessorBase<VariableIdentifierAttribute>
{
    protected override bool TryProcessValidation(AttributeData attribute, ref readonly PropertyProcessingContext context, ref readonly PropertyValidationTarget target, out PropertyValidationAspect? aspect)
    {
        if (!ValidateStringLikeTargetType(attribute, in context, in target))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new VariableIdentifierAspect();
        return true;
    }

    private sealed class VariableIdentifierAspect : PropertyValidationAspect
    {
        public override void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.NullAwareCondition($"!runtime.Environment.SyntaxFactory.IsValidIdentifier({model.StringContentExpression})")}})
            {
                {{model.Variables.Errors}}.Add({{CreateValidationError(model, rule: "valid_identifier", $"{model.TargetDescription} must be a valid variable identifier, but was '{{{model.DisplayExpression}}}'.")}});
            }
            """);
        }
    }
}
