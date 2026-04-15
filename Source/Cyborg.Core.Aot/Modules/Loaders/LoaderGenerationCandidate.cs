using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Loaders;

internal sealed record LoaderGenerationCandidate(LoaderGenerationModel? Model, ImmutableArray<Diagnostic> Diagnostics);