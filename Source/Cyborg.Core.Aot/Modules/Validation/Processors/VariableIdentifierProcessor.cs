using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class VariableIdentifierProcessor : AttributeProcessorBase<VariableIdentifierAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!ValidatePropertyType(attribute, in context, SpecialType.System_String))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new VariableIdentifierAspect();
        return true;
    }

    private sealed class VariableIdentifierAspect : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.AccessExpression}} is not null && !runtime.Environment.SyntaxFactory.IsValidIdentifier({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, rule: "valid_identifier", $"Property '{{nameof({model.AccessExpression})}}' must be a valid symbol name starting with a letter or underscore, followed by ., -, _, or alphanumeric characters.")}});
            }
            """);
        }
    }
}
