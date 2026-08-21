using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering;
using Cyborg.Shared.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Aspects;

internal static class PropertyValidationAspectExtensions
{
    extension(IPropertyValidationAspect self)
    {
        public void EmitValidation(
            ValidationSectionRenderer renderer,
            IndentedStringBuilder builder,
            PropertyModel property,
            string propertyAccessExpression,
            ValidationPath path)
        {
            PropertyValidationModel model = new(
                Property: property,
                ContractInfo: renderer.ContractInfo,
                DiagnosticsReporter: renderer.DiagnosticsReporter,
                Variables: renderer.Variables,
                AccessExpression: propertyAccessExpression,
                Path: path,
                TargetType: property.Symbol.Type,
                TargetNullableTypeName: property.NullableTypeName);
            self.EmitValidation(builder, model);
        }

        public void EmitCollectionElementValidation(
            ValidationSectionRenderer renderer,
            IndentedStringBuilder builder,
            PropertyModel property,
            string elementAccessExpression,
            ValidationPath path)
        {
            CollectionModel collection = property.Collection
                ?? throw new InvalidOperationException($"Property '{property.Name}' does not describe a collection.");
            PropertyValidationModel model = new(
                Property: property,
                ContractInfo: renderer.ContractInfo,
                DiagnosticsReporter: renderer.DiagnosticsReporter,
                Variables: renderer.Variables,
                AccessExpression: elementAccessExpression,
                Path: path,
                TargetType: collection.ElementType,
                TargetNullableTypeName: collection.ElementNullableTypeName);
            self.EmitValidation(builder, model);
        }
    }
}
