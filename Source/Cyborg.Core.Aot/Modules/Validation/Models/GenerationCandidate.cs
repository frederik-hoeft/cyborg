using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation.Attributess;

internal sealed record GenerationCandidate(string HintName, ModuleModel? Model, ImmutableArray<Diagnostic> Diagnostics);