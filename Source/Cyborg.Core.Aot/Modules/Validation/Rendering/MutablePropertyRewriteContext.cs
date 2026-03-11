using Cyborg.Core.Aot.Modules.Validation.Attributess;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal sealed record MutablePropertyRewriteContext
(
    PropertyModel Property,
    ValidationContractInfo ContractInfo,
    DiagnosticsReporter DiagnosticsReporter,
    string ModuleVariable,
    string PropertyAccessExpression
) : PropertyRewriteContext(Property, ContractInfo, DiagnosticsReporter, ModuleVariable, PropertyAccessExpression)
{
    public void SetProperty(PropertyModel newProperty) => Property = newProperty;
}