using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Rendering;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record PropertyValidationModel
(
    PropertyModel Property,
    ValidationContractInfo ContractInfo,
    DiagnosticsReporter DiagnosticsReporter,
    ValidationSectionRenderer.ValidationVariables Variables,
    string AccessExpression,
    string ErrorPropertyAccessExpression,
    ITypeSymbol TargetType,
    string TargetNullableTypeName,
    bool IsCollectionElement,
    string TargetDescription = "Property"
)
{
    public string PropertyNameExpression => $"nameof({ErrorPropertyAccessExpression})";

    public bool IsTaggedString => TargetType.EqualsIgnoreNullability(ContractInfo.TaggedString);

    public string StringContentExpression => IsTaggedString
        ? TargetType.CanEverBeNull
            ? $"{AccessExpression}?.Value"
            : $"{AccessExpression}.Value"
        : AccessExpression;

    public string DisplayExpression => IsTaggedString
        ? $"{Variables.Context}.Render({AccessExpression})"
        : AccessExpression;

    public string NullAwareCondition(string condition) =>
        TargetType.CanEverBeNull ? $"{AccessExpression} is not null && {condition}" : condition;
}
