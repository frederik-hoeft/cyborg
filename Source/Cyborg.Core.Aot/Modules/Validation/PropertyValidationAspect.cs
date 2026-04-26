using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation;

internal abstract class PropertyValidationAspect
{
    public abstract bool EnsuresDefault { get; }

    public virtual string? RewriteOverrideResolutionExpression(PropertyRewriteContext context, string? currentExpression, string rootPathExpression) => currentExpression;

    public virtual string? RewriteDefaultAssignmentExpression(PropertyRewriteContext context, string? currentExpression) => currentExpression;

    protected virtual void EmitValidation(IndentedStringBuilder builder, ModulePropertyModel model)
    {
    }

    public void EmitValidation(IndentedStringBuilder builder, ValidationContractInfo contractInfo, DiagnosticsReporter diagnosticsReporter, PropertyModel property, string moduleVariableName, string propertyAccessExpression)
    {
        ModulePropertyModel model = new(property, contractInfo, diagnosticsReporter, moduleVariableName, propertyAccessExpression);
        EmitValidation(builder, model);
    }

    protected static string CreateValidationError(ModulePropertyModel model, string rule, string message) =>
        $"""
        new {model.ContractInfo.ValidationError.RenderGlobal()}(nameof({model.AccessExpression}), "{rule}", $"{message}")
        """;

    protected sealed record ModulePropertyModel(PropertyModel Property, ValidationContractInfo ContractInfo, DiagnosticsReporter DiagnosticsReporter, string ModuleVariable, string? ExplicitAccessExpression)
    {
        public string AccessExpression => field ??= ExplicitAccessExpression ?? $"{ModuleVariable}.{Property.Name}";
    }
}
