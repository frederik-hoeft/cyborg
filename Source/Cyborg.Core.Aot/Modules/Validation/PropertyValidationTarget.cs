using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal readonly record struct PropertyValidationTarget(ITypeSymbol Type, bool IsCollectionElement);
