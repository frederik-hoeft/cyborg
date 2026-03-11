using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Attributess;

internal sealed record ModuleModel(
    string Namespace,
    string TypeName,
    string FullyQualifiedTypeName,
    string HintName,
    ImmutableArray<ContainingTypeModel> ContainingTypes,
    ImmutableArray<PropertyModel> Properties);