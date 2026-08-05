using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation;

internal record PropertyRewriteContext
{
    public PropertyModel Property { get; protected set; }

    public ValidationContractInfo ContractInfo { get; init; }

    public string ModuleVariable { get; init; }

    public string ContextVariable { get; init; }

    public string PropertyAccessExpression { get; init; }

    public DiagnosticsReporter DiagnosticsReporter { get; init; }

    public PropertyRewriteContext(PropertyModel property,
        ValidationContractInfo contractInfo,
        DiagnosticsReporter diagnosticsReporter,
        string moduleVariable,
        string contextVariable,
        string propertyAccessExpression)
    {
        ContractInfo = contractInfo;
        ModuleVariable = moduleVariable;
        ContextVariable = contextVariable;
        DiagnosticsReporter = diagnosticsReporter;
        PropertyAccessExpression = propertyAccessExpression;
        Property = property;
    }
}
