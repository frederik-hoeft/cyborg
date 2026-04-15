using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Composition;

internal sealed record DecompositionGenerationCandidate(DecompositionGenerationModel? Model, ImmutableArray<Diagnostic> Diagnostics);
