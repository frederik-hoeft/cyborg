using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering;
using Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;
using Cyborg.Shared.Text;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class RequiredProcessor : PropertyValidationProcessorBase<RequiredAttribute>
{
    protected override bool TryProcessValidation(AttributeData attribute, ref readonly PropertyProcessingContext context, ref readonly PropertyValidationTarget target, out PropertyValidationAspect? aspect)
    {
        _ = CollectionTypeInspector.TryDescribe(context.Compilation, target.Type, out CollectionShape? collectionShape);
        aspect = new RequiredValidationAspect(collectionShape);
        return true;
    }

    private sealed class RequiredValidationAspect(CollectionShape? collectionShape) : PropertyValidationAspect
    {
        public override void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model)
        {
            string condition;
            if (model.TargetType.IsStringLike(model.ContractInfo.TaggedString))
            {
                condition = $"string.{nameof(string.IsNullOrWhiteSpace)}({model.StringContentExpression})";
            }
            else if (collectionShape is not null)
            {
                ValueAccess access = collectionShape.Renderer.Access(model.AccessExpression);
                condition = access.RequiresGuard
                    ? access.MissingExpression
                    : CreateDefaultValueCondition(model);
            }
            else
            {
                condition = CreateDefaultValueCondition(model);
            }

            builder.AppendBlock(
            $$"""
            if ({{condition}})
            {
                {{model.Variables.Errors}}.Add({{CreateValidationError(model, "required", $"{model.TargetDescription} is required.")}});
            }
            """);
        }

        private static string CreateDefaultValueCondition(PropertyValidationModel model)
        {
            string comparer = KnownTypes.DefaultEqualityComparerOfT(model.TargetNullableTypeName);
            return $"{comparer}.Equals({model.AccessExpression}, default!)";
        }
    }
}
