# Cyborg Repository Instructions

Cyborg is a .NET 10 application for modular, JSON-configured workflow orchestration with native AOT compilation support. Workflows are immutable module trees that are loaded through source-generated JSON metadata, prepared through generated validation code, and executed inside hierarchical runtime environments.

## Start Here

Before changing implementation code:

1. Read [`/code-style.md`](/code-style.md).
2. Read the architecture hub at [`/docs/architecture.md`](/docs/architecture.md).
3. Read the subsystem document relevant to the change and inspect nearby production code and tests.
4. Treat the current architecture documentation and implementation as the source of truth. Update the documentation when a change alters a documented contract.

Important architecture references:

| Topic | Document |
|-------|----------|
| System structure and runtime interactions | [`/docs/architecture/architecture-overview.md`](/docs/architecture/architecture-overview.md) |
| Interpolation and override-resolution contract | [`/docs/architecture/interpolation.md`](/docs/architecture/interpolation.md) |
| Source generators | [`/docs/architecture/source-generators.md`](/docs/architecture/source-generators.md) |
| Validation/default/override attributes | [`/docs/architecture/validation-attributes-reference.md`](/docs/architecture/validation-attributes-reference.md) |
| Built-in modules | [`/docs/architecture/modules-reference.md`](/docs/architecture/modules-reference.md) |
| Dynamic values | [`/docs/architecture/dynamic-values-reference.md`](/docs/architecture/dynamic-values-reference.md) |
| Templates | [`/docs/architecture/templates-reference.md`](/docs/architecture/templates-reference.md) |
| Module test infrastructure | [`/docs/architecture/module-testing.md`](/docs/architecture/module-testing.md) |

## Solution Structure

The solution is under `Source/Cyborg.slnx`.

| Project | Responsibility |
|---------|----------------|
| `Cyborg.Core` | Runtime abstractions, module execution, environment scoping, configuration, parsing, process execution, metrics, security, and shared services. It must not contain module-specific behavior. |
| `Cyborg.Core.Aot` | Roslyn incremental generators for module validation, loader factories, decomposition, and generator-contract bootstrapping. Targets `netstandard2.0` for analyzer hosting. |
| `Cyborg.Shared` | Source-shared utility code imported by both `Cyborg.Core` and `Cyborg.Core.Aot` without creating a runtime/analyzer project dependency. |
| `Cyborg.Core.TestAdapter` | Reusable production-backed module test runtime and higher-order test APIs. |
| `Cyborg.TestModules` | Minimal analyzer-consuming assembly for source-generator fixture models. |
| `Cyborg.Core.Tests` | Core runtime and infrastructure tests. |
| `Cyborg.Modules` | Built-in domain-agnostic modules and their generated code. |
| `Cyborg.Modules.Tests` | Tests for built-in modules and generated validation behavior. |
| `Cyborg.Modules.Borg` | Borg-specific modules, configuration types, parsers, and metrics. |
| `Cyborg.Modules.Borg.Tests` | Borg-specific tests. |
| `Cyborg.Cli` | ConsoleAppFramework entry point and Jab composition root. |

## Architectural Invariants

### Module model

A normal executable module has three production types:

1. A sealed partial configuration record marked `[GeneratedModuleValidation]`, inheriting from `ModuleBase`, implementing `IModule`, and exposing a versioned static `ModuleId`.
2. A sealed worker inheriting from `ModuleWorker<TModule>`, receiving an `IWorkerContext<TModule>`, and implementing `ExecuteAsync`.
3. A sealed partial loader marked `[GeneratedModuleLoaderFactory]` and inheriting from `ModuleLoader<TWorker, TModule>`.

`ModuleContext` is the execution envelope. It combines the module with environment selection, an optional configuration module, and optional requirements. `ModuleReference` is the polymorphic composition mechanism used for nested module trees.

Module IDs are versioned, dot-separated identifiers such as `cyborg.modules.subprocess.v1`. JSON property names use snake case. Polymorphic loading is registry-based and must remain native-AOT-compatible.

### Generated preparation and validation

The generated module pipeline has a fixed order:

1. Apply defaults.
2. Resolve or select overrides.
3. Reapply defaults.
4. Interpolate eligible textual values (`string` and `TaggedString`).
5. Validate constraints.

Each phase returns transformed records through `with` expressions. Do not mutate the deserialized module instance.

`string` and `TaggedString` overrides are selected as stored values without evaluating their contents. Non-textual overrides use full typed resolution. This distinction is required so `[IgnoreInterpolation]` applies equally to JSON values, defaults, and overrides, and so generated preparation does not accidentally perform an extra interpolation pass.

The worker receives the validated module. Do not repeat generated interpolation in worker code. Manually call `runtime.Environment.Interpolate(...)` only for values whose evaluation was intentionally deferred, normally through `[IgnoreInterpolation]`.

### Runtime environments, resolution, and interpolation

Environment values are late-bound:

- `SetVariable(...)` stores values unchanged.
- `TryResolveVariable(...)` and `Interpolate(...)` are complete evaluation boundaries.
- Unresolved ordinary expressions remain unchanged.
- Cyclic resolution throws `InvalidOperationException`.

Supported ordinary expressions are:

| Syntax | Meaning |
|--------|---------|
| `${identifier}` | Resolve relative to the scope where the expression is encountered. |
| `${@identifier}` | Resolve relative to the original resolution entry point. |
| `${@}` | Resolve the current scope namespace. |
| `${@@}` | Resolve the original entry-point namespace. |

`${#...}` escapes one interpolation pass. Each pass removes exactly one leading `#` and does not rescan the newly exposed expression during that pass.

The effective module namespace uses `Name`, then `Group`, then `ModuleId`. Override lookup uses this precedence:

1. Module `Name`.
2. Module `Group`.
3. Module ID.
4. Override-resolution tags in their declared order.

`ModuleBase.Name` and `ModuleBase.Group` define structural identity before validation and therefore opt out of normal override and interpolation processing.

### Source generators and native AOT

Production paths must remain trim-safe and native-AOT-compatible. Prefer source generation, explicit registration, and compile-time metadata over reflection or runtime type discovery.

Do not edit generated output. Change the source generator, processor/aspect, contract registration, annotated model, or source-generation metadata that owns the behavior.

Generator targets have structural requirements:

- `[GeneratedModuleValidation]`: partial record.
- `[GeneratedModuleLoaderFactory]`: partial loader with the expected `ModuleLoader<TWorker, TModule>` base.
- `[GeneratedDecomposition]`: partial record or class.
- `[Validatable]`: nested record class or record struct participating recursively in defaults, overrides, interpolation, constraints, and generated description traversal.

Generator-emitted references should use fully qualified global type names. Preserve incremental-generator value semantics and avoid carrying mutable or identity-based state through incremental pipelines.

When changing generator behavior, add or update diagnostics and regression coverage for the generated shape, nullability, collection materialization, recursion, and invalid attribute usage as applicable.

### Dynamic values and decomposition

A dynamic value entry contains a non-null `key` and exactly one non-null typed-value property. Structural errors belong in the converter because dynamic values are also used outside module-validation paths. Semantic constraints belong in generated validation where applicable.

New dynamic types require an `IDynamicValueProvider`, explicit service registration, and the appropriate source-generated JSON metadata. Use `[GeneratedDecomposition]` when a typed model must expose hierarchical environment paths through `IDecomposable`.

### Dependency injection and serialization

Jab service-provider modules are the compile-time DI registration surface. Module loaders are registered as `IModuleLoader` implementations in the appropriate service module, such as `ICyborgModuleServices` or `ICyborgBorgServices`.

Every serializable module/configuration type required at runtime must be enrolled in the appropriate `JsonSerializerContext`. Do not introduce reflection-based serialization as a shortcut.

## Adding or Changing a Module

For a new module or a new version of an existing module:

1. Add the module record, worker, and generated loader in the appropriate project and namespace.
2. Apply validation, defaulting, override, and interpolation-control attributes to the configuration model instead of duplicating preparation logic in the worker.
3. Register the loader in the appropriate Jab service module.
4. Add the module/configuration type to the appropriate source-generated JSON serializer context.
5. Add focused tests through the production-backed module test adapter.
6. Update the module reference and any affected architecture documentation.

Do not silently change the JSON contract or behavior of an existing versioned module ID. Add a new version or document and justify compatibility when the external contract changes.

## Testing

Prefer the higher-order APIs exposed by `CyborgTestBase` and the domain test bases for module tests. They exercise production deserialization, generated preparation, validation, runtime execution, and artifact publication with less duplicated setup.

Use `Cyborg.TestModules` for fixture records that must directly consume `Cyborg.Core.Aot`. Do not enroll `Cyborg.Modules.Tests` itself in the validation analyzer: it is an `InternalsVisibleTo` target of `Cyborg.Core`, and emitting another copy of the internal generator framework types there creates conflicting definitions.

Keep tests in the matching layer:

- core behavior in `Cyborg.Core.Tests`;
- domain-agnostic modules in `Cyborg.Modules.Tests`;
- Borg-specific behavior in `Cyborg.Modules.Borg.Tests`;
- generator-only model shapes in `Cyborg.TestModules`, asserted from the relevant test project.

## Code Style

Follow `/code-style.md` and `.editorconfig`. In particular:

- Use explicit types instead of `var`, except for truly anonymous types.
- Use Allman braces and four-space indentation.
- Keep `using` directives outside file-scoped namespaces and sort them alphabetically.
- Always specify visibility.
- Use `_camelCase` for non-public instance fields, `s_camelCase` for static fields, and `SCREAMING_SNAKE_CASE` for compile-time constants.
- Suffix task-returning methods with `Async`.
- Use language keywords such as `int` and `string` instead of BCL aliases.
- Seal or make static internal/private types unless derivation is required.
- Use nullable annotations and the appropriate nullability attributes for `Try...` APIs.
- Prefer `nameof(...)`, `string.Empty`, target-typed `new()` with an explicit left-hand type, and named arguments for unclear constants.
- Use Unicode escape sequences rather than literal non-ASCII source characters.
- Keep one top-level type per file unless tightly coupled types clearly benefit from co-location.
- Keep source lines at or below 196 characters. Do not wrap code to an 80-column convention; keep related parameter and argument lists together when they remain readable within 196 characters, and do not introduce context objects or temporary abstractions solely to shorten lines.

Match the established style in adjacent code. Avoid unrelated cleanup in focused changes.

## Documentation Style

Most documents under `/docs/architecture/` are architecture documentation, not exhaustive API references or implementation notes. Write them to help a new contributor build an accurate mental model of the system from the documentation alone.

When writing or updating architecture documentation:

- Describe the **current stable architecture in present tense**. Do not write documentation as a changelog with phrases such as “now does”, “was changed to”, or “previously used” unless migration history or compatibility behavior is itself part of the contract. When a design changes, rewrite the stale description into the new steady-state model instead of layering historical caveats on top of it.
- Focus on **architectural components, responsibilities, contracts, data flow, lifecycle/phase ordering, and cross-subsystem interactions**. Explain why boundaries exist when that distinction is important to using or extending the system correctly.
- Prefer a **coherent subsystem narrative** over a catalog of classes, methods, or fields. Mention concrete implementation types when they are important architectural anchors, extension points, ownership boundaries, or useful navigation aids; do not enumerate incidental helpers or every method signature.
- Keep implementation details out of architecture docs unless they establish an externally relevant invariant, explain a non-obvious interaction, constrain extension/AOT behavior, or are necessary to understand the architecture. Leave local algorithms and ordinary control-flow details to the code and tests.
- Preserve the established **structure, tone, verbosity, and abstraction level** of nearby up-to-date documentation. Make targeted edits to stale sections rather than broadly rewriting unrelated material.
- Update all affected descriptions when a change creates a new cross-dependency or changes the interaction between documented subsystems. Avoid fixing one document while leaving a contradictory contract elsewhere.
- Avoid over-documenting temporary implementation states. Prefer the durable conceptual model and important API contracts over details that are likely to change without affecting architecture.
- Use examples, signatures, and snippets selectively to clarify a contract or data flow, not as a substitute for explaining the model.
- Cross-link existing focused documents instead of duplicating large explanations. Keep the architecture overview broad enough to connect subsystems, and put subsystem-specific detail in the corresponding focused document.
- Keep the README user-facing and task-oriented. Introduce major capabilities, configuration surfaces, and extension points there, but defer architectural depth to `/docs/architecture/`.

For documentation cleanup, prefer small edits that remove accumulated change-over-time artifacts, inconsistent terminology, or misplaced implementation trivia while retaining sections that are already accurate and well-structured.

## Build and Validation

Run commands from the repository root:

```bash
dotnet restore Source/Cyborg.slnx
dotnet build Source/Cyborg.slnx --no-restore --configuration Release
dotnet test Source/Cyborg.slnx --no-build --configuration Release
```

For focused iteration, run the affected test project directly, then run the full solution tests before completing a substantive implementation change.

For documentation-only changes, inspect the rendered Markdown, verify links and paths, and report that no build was required rather than claiming unrun validation.

## Change Discipline

- Keep changes scoped to the requested behavior.
- Preserve layer boundaries and native-AOT constraints.
- Preserve the generated validation phase order and the separation between override selection and expression evaluation.
- Add regression tests for behavioral fixes.
- Update architecture documentation when contracts, phases, registration requirements, or cross-subsystem interactions change.
- Do not guess at undocumented behavior when the implementation or tests can establish the contract.
