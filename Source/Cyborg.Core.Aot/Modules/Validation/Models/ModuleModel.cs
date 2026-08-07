using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal sealed record ModuleModel(INamedTypeSymbol ModuleSymbol, string Namespace, string TypeName, string FullyQualifiedTypeName, string HintName,
    ImmutableArray<ContainingTypeModel> ContainingTypes, ImmutableArray<PropertyModel> Properties);
