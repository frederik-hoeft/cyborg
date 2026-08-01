# Cyborg System Architecture

Cyborg is a .NET 10, JSON-configured backup-orchestration application designed for native AOT publication. Its architecture separates immutable module configuration from execution logic, uses source generation for reflection-free runtime contracts, and communicates between modules through hierarchical runtime environments.

For detailed reference material, see:

- [Module Reference](modules-reference.md) — Built-in modules and their configuration
- [Dynamic Values Reference](dynamic-values-reference.md) — Typed dynamic values and providers
- [Templates Reference](templates-reference.md) — Template-module behavior and usage
- [Source Generators](source-generators.md) — Generated validation, loading, and decomposition code
- [Validation Attributes Reference](validation-attributes-reference.md) — Validation, defaulting, override, and interpolation controls
- [Module Testing](module-testing.md) — Production-backed test infrastructure

## Project structure

| Project | Target | Responsibility |
|---|---|---|
| `Cyborg.Core` | net10.0 | Runtime abstractions, environments, configuration, validation contracts, parsing, security, and shared services |
| `Cyborg.Core.Aot` | netstandard2.0 | Roslyn incremental generators distributed as analyzers |
| `Cyborg.Modules` | net10.0 | Built-in domain-agnostic modules |
| `Cyborg.Modules.Borg` | net10.0 | Borg-specific modules and parsers |
| `Cyborg.Cli` | net10.0 | CLI entry point and application composition root |

`Cyborg.Core.Aot` discovers registered runtime contract symbols from the consuming compilation and emits code that references them through fully qualified names. `Cyborg.Modules` and `Cyborg.Modules.Borg` expose Jab service-provider modules that the CLI imports into its compile-time DI graph.

## Module model

Each module consists of three responsibilities:

| Type | Responsibility |
|---|---|
| Module record | Immutable configuration; inherits `ModuleBase` and implements `IModule` |
| Worker | Execution logic; inherits `ModuleWorker<TModule>` |
| Loader | Polymorphic JSON deserialization and worker construction |

A module record is never mutated in place. Before execution, generated preparation stages create transformed record copies containing defaults, runtime overrides, and interpolated values. Workers receive only the validated module instance.

### Module references and contexts

A `ModuleReference` is encoded as a JSON object whose single property name is the versioned module ID. The loader registry uses that ID to select the corresponding `IModuleLoader` without reflection-based polymorphic deserialization.

A `ModuleContext` wraps a module reference with execution context:

- environment scope and optional environment name;
- an optional configuration module that populates the environment;
- optional requirements evaluated before the main module;
- the module to execute.

Nested module references allow modules to compose execution trees without coupling to concrete child-module types.

## Execution lifecycle

`ModuleWorker<TModule>` coordinates environment binding, generated preparation and validation, optional custom validation, execution, and artifact publication.

### Generated preparation and validation pipeline

`ValidateAsync` orchestrates the current generated pipeline in this order:

1. **Apply defaults** — Apply `[DefaultValue<T>]`, `[DefaultInstance]`, `[DefaultInstanceFactory]`, and `[DefaultTimeSpan]` recursively to eligible properties, nested `[Validatable]` records, and supported collection elements.
2. **Resolve overrides** — Replace eligible properties from runtime environment values. `[IgnoreOverride]` suppresses the annotated property; `Recurse = true` suppresses its complete subtree.
3. **Reapply defaults** — Give values introduced by overrides the same default semantics as deserialized values.
4. **Interpolate strings** — Apply `runtime.Environment.Interpolate(...)` to eligible strings, including strings in nested validatable records and supported collections. `[IgnoreInterpolation]` preserves strings that require later context-specific interpolation.
5. **Validate constraints** — Evaluate required, range, length, filesystem, enum, regex, grammar, and related validation aspects and return a `ValidationResult<TModule>`.

The generator emits two explicit `IModule<TModule>` methods (`ApplyDefaultsAsync` and `ResolveOverridesAsync`), the private static `__ApplyInterpolation` helper, and public `ValidateAsync` orchestration.

`ModuleBase.Name` and `ModuleBase.Group` opt out of both override resolution and interpolation because runtime environment binding consumes these structural identity values before validation begins. `AssertModule.Message` opts out of generated interpolation and is interpolated by its worker only after the assertion child has executed, allowing the message to reference child artifacts.

### Collection semantics

Generated collection traversal uses a common enumeration guard based on the actual collection shape:

- null reference collections are skipped;
- nullable value-type collections are unwrapped only when present;
- default `ImmutableArray<T>` values are not enumerated;
- `default(ImmutableArray<T>)` remains distinct from `ImmutableArray<T>.Empty`;
- invalid required collections can therefore produce validation errors instead of throwing during recursive element validation.

Supported collection rewrites materialize arrays, lists, immutable arrays, supported collection interfaces, and compatible concrete collection types according to `CollectionMaterializationKind`.

### Custom validation and execution

After generated validation succeeds, a worker may run custom validation through its callback hook. `EnsureValid()` prevents `ExecuteAsync` from running when errors exist.

Execution returns an `IModuleExecutionResult` with a `ModuleExitStatus` (`Success`, `Failed`, `Skipped`, or `Canceled`) and an artifact scope. Worker result builders and `runtime.Exit(...)` finalize the result and publish configured artifacts.

## Runtime hierarchy and environments

`RootModuleRuntime` owns the global environment and named-environment registry. Nested module execution creates scoped runtimes that carry an `IRuntimeEnvironment` while delegating root-level registration and lookup to the runtime hierarchy.

The effective module namespace is selected from `Name`, then `Group`, then `ModuleId`. It controls override lookup, self-references, artifact paths, and default artifact namespaces.

### Environment scopes

| Scope | Behavior |
|---|---|
| `InheritParent` | New environment with fallback to the immediate parent |
| `Isolated` | New environment without inheritance |
| `Global` | Execute directly in the global environment |
| `InheritGlobal` | New environment inheriting only from global |
| `Parent` | Reuse the parent environment |
| `Current` | Reuse the current environment |
| `Reference` | Use a previously registered named environment |

Named non-transient environments are registered at the root and can later be selected through `Reference` scope.

### Variable resolution and interpolation

Runtime environments support:

- typed direct lookup;
- parent fallback for inherited environments;
- `${...}` indirection;
- mixed literal/placeholder interpolation;
- current-scope and entry-point self references;
- decomposable-object member traversal;
- cycle detection.

Override resolution is a typed property-replacement mechanism. String interpolation is a later, separate phase. Keeping the two contracts separate allows a property to reject overrides while still being interpolated, or to preserve its raw string for worker-time interpolation.

## Dynamic values and configuration

Configuration maps deserialize entries through registered `IDynamicValueProvider` implementations. Each dynamic entry requires a non-null key and value during parsing; malformed entries fail immediately with `JsonException`, even outside module-validation call sites. Empty or whitespace keys are subsequently rejected by `[Required]` when the model participates in generated validation.

Custom dynamic value types may use `[GeneratedDecomposition]` to expose typed properties through hierarchical environment paths.

## Artifact publication

Module results can expose decomposable values as artifacts. Artifact configuration controls namespace, destination scope, and publication behavior. Published values are available to later modules through runtime environment lookup, subject to scope and namespace rules.

Because child artifacts may not exist during pre-execution validation, values that intentionally reference them must opt out of generated interpolation and resolve after child execution.

## Supporting infrastructure

### Parsing

The parser-combinator subsystem builds grammars from sequence, alternative, optional, and regex-backed terminal parsers. It produces typed syntax trees consumed through visitors. The same infrastructure supports subprocess-output parsing and `[MatchesGrammar]` validation.

### Subprocess execution

`IChildProcessDispatcher` executes `ProcessStartInfo` instances asynchronously and returns exit code, standard output, and standard error. Arguments are passed through `ProcessStartInfo.ArgumentList`, not shell-concatenated command strings.

### Metrics

Modules register Prometheus-compatible counters, gauges, and untyped metrics through `IMetricsCollector`. The CLI writes collected samples in exposition format after execution.

## Security and AOT constraints

Cyborg treats external configuration as executable orchestration input. Configured trust policies validate ownership and permissions before external files are deserialized. Enforcement can reject, log, or disable trust-policy failures according to deployment settings.

Cross-cutting constraints include:

- source-generated JSON metadata instead of reflection-based serialization;
- Jab compile-time dependency injection;
- source-generated module loaders, validation, and decomposition;
- array-based subprocess argument passing;
- validation before execution;
- trim-safe, AOT-compatible runtime contracts.

These constraints are reinforced by project build settings and analyzer diagnostics.
