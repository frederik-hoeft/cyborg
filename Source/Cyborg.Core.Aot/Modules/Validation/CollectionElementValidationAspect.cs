using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;

namespace Cyborg.Core.Aot.Modules.Validation;

internal sealed record CollectionElementValidationAspect(PropertyValidationAspect ValidationAspect) : IPropertyValidationAspect
{
    public void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model) => ValidationAspect.EmitValidation(builder, model);
}
