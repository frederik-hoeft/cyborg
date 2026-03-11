using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Loaders;

internal sealed record LoaderGenerationModel(
    string Namespace,
    LoaderContractInfo ContractInfo,
    INamedTypeSymbol ClassSymbol,
    IMethodSymbol WorkerConstructor,
    INamedTypeSymbol ModuleWorkerType,
    ITypeSymbol ModuleType,
    string? MethodName,
    string MethodModifiers);