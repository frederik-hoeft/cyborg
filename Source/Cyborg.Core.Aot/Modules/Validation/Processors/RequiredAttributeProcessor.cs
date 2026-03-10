using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class RequiredAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(RequiredAttribute).FullName;

    public bool TryProcess(PropertyAttributeProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        _ = context;
        _ = attribute;
        aspect = new RequiredValidationAspect();
        return true;
    }

    private sealed class RequiredValidationAspect : PropertyValidationAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            string comparer = LiteralExpressionFactory.GetDefaultEqualityComparer(model.Property.NullableTypeName);

            builder.AppendBlock(
            $$"""
            if ({{comparer}}.Equals({{model.AccessExpression}}, default!))
            {
                errors.Add({{CreateValidationError(model, "required", $"Property '{{nameof({model.AccessExpression})}}' is required.")}});
            }
            """);
        }
    }
}
