using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record PropertyModel(
    IPropertySymbol Symbol,
    string Name,
    string NullableTypeName,
    string NonNullableTypeName,
    bool IsNullable,
    bool IsValidatableType,
    ImmutableArray<PropertyValidationAspect> Aspects,
    ImmutableArray<PropertyModel> Children)
{
    public bool HasDefault => Aspects.Any(static a => a.EnsuresDefault);
}