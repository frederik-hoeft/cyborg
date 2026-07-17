using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class FileNameAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(FileNameAttribute).FullName;

    public bool TryProcess(PropertyProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        aspect = null;
        if (attribute.AttributeClass is null)
        {
            return true;
        }
        if (context.Property.Type.SpecialType is not SpecialType.System_String)
        {
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name, nameof(FileNameAttribute), nameof(String));
            return false;
        }
        aspect = new FileNameValidationAspect();
        return true;
    }

    private sealed class FileNameValidationAspect : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.AccessExpression}} is not null && !{{KnownTypes.ValidationRuntimeHelpers}}.IsValidFileName({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, rule: "file_name", $"Property '{{nameof({model.AccessExpression})}}' must be a valid file name, but was '{{{model.AccessExpression}}}'")}});
            }
            """);
        }
    }
}
