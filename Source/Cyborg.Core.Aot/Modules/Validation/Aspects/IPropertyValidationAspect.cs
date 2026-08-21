using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Aspects;

internal interface IPropertyValidationAspect : IPropertyAspect
{
    void EmitValidation(IndentedStringBuilder builder, PropertyValidationModel model);
}
