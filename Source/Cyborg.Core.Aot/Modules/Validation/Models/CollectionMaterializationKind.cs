namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal enum CollectionMaterializationKind
{
    None = 0,
    UseList,
    UseArray,
    UseImmutableArray,
    ConstructFromList,
    ParameterlessAdd,
}
