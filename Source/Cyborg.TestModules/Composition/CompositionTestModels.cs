using Cyborg.Core.Aot.Modules.Composition;

namespace Cyborg.TestModules.Composition;

[GeneratedDecomposition]
public sealed partial record CompositionLeafOptions(string Value, int Count);

[GeneratedDecomposition]
public sealed partial record CompositionNestedOptions(CompositionLeafOptions Nested, string? Label);

[GeneratedDecomposition]
public sealed partial record CompositionOptionalNestedOptions(CompositionLeafOptions? Nested, string Name);
