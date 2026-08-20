using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class RequiredProcessor : PropertyValidationProcessorBase<RequiredAttribute>
{
    protected override bool TryProcessValidation(AttributeData attribute, ref readonly PropertyProcessingContext context, ref readonly PropertyValidationTarget target, out PropertyValidationAspect? aspect)
    {
        aspect = new RequiredValidationAspect();
        return true;
    }

    private sealed class RequiredValidationAspect : PropertyValidationAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            if (TypeSymbolHelpers.IsStringLikeType(model.TargetType, model.ContractInfo))
            {
                builder.AppendLine($"if (string.{nameof(string.IsNullOrWhiteSpace)}({model.StringContentExpression}))");
            }
            else
            {
                string comparer = KnownTypes.DefaultEqualityComparerOfT(model.TargetNullableTypeName);
                builder.AppendLine($"if ({comparer}.Equals({model.AccessExpression}, default!))");
            }
            builder.AppendBlock(
            $$"""
            {
                errors.Add({{CreateValidationError(model, "required", $"{model.TargetDescription} '{{{model.PropertyNameExpression}}}' is required.")}});
            }
            """);
        }
    }
}
