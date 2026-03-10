using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Loaders;

internal sealed record LoaderGenerationModel(
    string Namespace,
    INamedTypeSymbol ClassSymbol,
    IMethodSymbol WorkerConstructor,
    INamedTypeSymbol ModuleWorkerType,
    ITypeSymbol ModuleType,
    string? MethodName,
    string MethodModifiers);