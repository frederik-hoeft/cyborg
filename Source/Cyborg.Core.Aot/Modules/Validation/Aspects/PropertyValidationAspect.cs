using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Aspects;

internal abstract class PropertyValidationAspect : IPropertyValidationAspect
{
    protected static string CreateValidationError(PropertyValidationModel model, string rule, string message) =>
        $"""
        new {model.ContractInfo.ValidationError.RenderGlobal()}({model.PathExpression}, "{rule}", $"{message}")
        """;

    public abstract void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model);
}
