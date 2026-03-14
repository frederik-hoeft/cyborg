using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class RequiredAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(RequiredAttribute).FullName;

    public bool TryProcess(PropertyProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        _ = context;
        _ = attribute;
        aspect = new RequiredValidationAspect();
        return true;
    }

    private sealed class RequiredValidationAspect : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            string comparer = KnownTypes.DefaultEqualityComparerOfT(model.Property.NullableTypeName);

            if (model.Property.Symbol.Type.SpecialType is SpecialType.System_String)
            {
                builder.AppendLine($"if (string.{nameof(string.IsNullOrWhiteSpace)}({model.AccessExpression}))");
            }
            else
            {
                builder.AppendLine($"if ({comparer}.Equals({model.AccessExpression}, default!))");
            }
            builder.AppendBlock(
            $$"""
            {
                errors.Add({{CreateValidationError(model, "required", $"Property '{{nameof({model.AccessExpression})}}' is required.")}});
            }
            """);
        }
    }
}
