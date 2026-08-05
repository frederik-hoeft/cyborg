using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed record MutablePropertyRewriteContext
(
    PropertyModel Property,
    ValidationContractInfo ContractInfo,
    DiagnosticsReporter DiagnosticsReporter,
    string ModuleVariable,
    string ContextVariable,
    string PropertyAccessExpression
) : PropertyRewriteContext(Property, ContractInfo, DiagnosticsReporter, ModuleVariable, ContextVariable, PropertyAccessExpression)
{
    public void SetProperty(PropertyModel newProperty) => Property = newProperty;
}
