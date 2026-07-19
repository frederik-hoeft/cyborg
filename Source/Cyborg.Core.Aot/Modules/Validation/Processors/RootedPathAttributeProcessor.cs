using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class RootedPathAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(RootedPathAttribute).FullName;

    public bool TryProcess(PropertyProcessingContext context, AttributeData attribute, out PropertyAspect? aspect)
    {
        aspect = null;
        if (attribute.AttributeClass is null)
        {
            return true;
        }
        if (context.Property.Type.SpecialType is not SpecialType.System_String)
        {
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name, nameof(RootedPathAttribute), nameof(String));
            return false;
        }
        aspect = new RootedPathValidationAspect();
        return true;
    }

    private sealed class RootedPathValidationAspect : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.AccessExpression}} is not null && !{{KnownTypes.Path}}.IsPathRooted({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, rule: "rooted_path", $"Property '{{nameof({model.AccessExpression})}}' must be a rooted path, but was '{{{model.AccessExpression}}}'")}});
            }
            """);
        }
    }
}
