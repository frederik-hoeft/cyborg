using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record PropertyModel(
    string Name,
    string TypeName,
    bool IsNullable,
    bool IsValidatableType,
    ImmutableArray<PropertyValidationAspect> Aspects,
    ImmutableArray<PropertyModel> Children);
