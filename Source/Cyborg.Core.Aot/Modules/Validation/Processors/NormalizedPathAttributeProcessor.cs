using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class NormalizedPathAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(NormalizedPathAttribute).FullName;

    public bool TryProcess(PropertyProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        aspect = null;
        if (attribute.AttributeClass is null)
        {
            return true;
        }
        if (context.Property.Type.SpecialType is not SpecialType.System_String)
        {
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name, nameof(NormalizedPathAttribute), nameof(String));
            return false;
        }
        aspect = new NormalizedPathValidationAspect();
        return true;
    }

    private sealed class NormalizedPathValidationAspect : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.AccessExpression}} is not null && !{{KnownTypes.Path}}.GetFullPath({{model.AccessExpression}}).Equals({{model.AccessExpression}}, {{KnownTypes.StringComparison}}.Ordinal))
            {
                errors.Add({{CreateValidationError(model, rule: "normalized_path", $"Property '{{nameof({model.AccessExpression})}}' must be a normalized path, but was '{{{model.AccessExpression}}}'")}});
            }
            """);
        }
    }
}
