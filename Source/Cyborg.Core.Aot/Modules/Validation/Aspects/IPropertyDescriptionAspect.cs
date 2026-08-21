using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Aspects;

internal interface IPropertyDescriptionAspect : IPropertyAspect
{
    void RegisterDescriptorHints(
        List<string> hints,
        ValidationContractInfo contractInfo,
        DiagnosticsReporter diagnosticsReporter,
        PropertyModel property);
}
