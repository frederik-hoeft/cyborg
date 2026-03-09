using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record PropertyModel(
    string Name,
    string TypeName,
    ImmutableArray<PropertyValidationAspect> Aspects);
