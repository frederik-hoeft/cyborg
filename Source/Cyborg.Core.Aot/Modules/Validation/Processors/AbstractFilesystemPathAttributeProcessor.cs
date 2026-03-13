using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal abstract class FilesystemPathAttributeProcessor<TAttribute> : IPropertyAttributeProcessor where TAttribute : Attribute
{
    public string AttributeMetadataName => typeof(TAttribute).FullName;

    protected abstract string AttributeName { get; }

    protected abstract string ErrorCode { get; }

    protected abstract string PathKindDisplayName { get; }

    protected abstract string BuildExistsExpression();

    public bool TryProcess(PropertyAttributeProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        aspect = null;
        if (attribute.AttributeClass is null)
        {
            return true;
        }
        if (context.Property.Type.SpecialType is not SpecialType.System_String)
        {
            context.Report(ValidationGeneratorDiagnostics.TypeMismatch, context.Property.Name, context.ContainingType.Name, AttributeName, nameof(String));
            return false;
        }
        aspect = new FilesystemPathValidationAspect(
            ErrorCode,
            PathKindDisplayName,
            BuildExistsExpression());
        return true;
    }

    private sealed class FilesystemPathValidationAspect(string errorCode, string pathKindDisplayName, string existsExpression) : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

        protected override void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
        {
            builder.AppendBlock(
            $$"""
            if ({{model.AccessExpression}} is not null && !{{existsExpression}}({{model.AccessExpression}}))
            {
                errors.Add({{CreateValidationError(model, errorCode, $"Property '{{nameof({model.AccessExpression})}}' requires an existing {pathKindDisplayName} at '{{{model.AccessExpression}}}'.")}});
            }
            """);
        }
    }
}
