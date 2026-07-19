using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class UnrootedPathAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(UnrootedPathAttribute).FullName;

    public bool TryProcess(PropertyProcessingContext context, AttributeData attribute, out PropertyAspect? aspect)
    {
        aspect = null;
        if (attribute.AttributeClass is null)
        {
            return true;
        }
        if (context.Property.Type.SpecialType is not SpecialType.System_String)
        {
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name, nameof(UnrootedPathAttribute), nameof(String));
            return false;
        }
        aspect = new UnrootedPathValidationAspect();
        return true;
    }

    private sealed class UnrootedPathValidationAspect : PropertyAspect
    {
        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.AccessExpression}} is not null && {{KnownTypes.Path}}.IsPathRooted({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, rule: "unrooted_path", $"Property '{{nameof({model.AccessExpression})}}' must be an unrooted path, but was '{{{model.AccessExpression}}}'")}});
            }
            """);
        }
    }
}
