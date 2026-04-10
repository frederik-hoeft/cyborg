# Source Generators

This document describes the source-generator layer in `Cyborg.Core.Aot`.

The generator package exists to keep the runtime and module model friendly to native AOT, trimming, and code review. Instead of solving module validation, decomposition, and worker construction through reflection or repetitive handwritten boilerplate, Cyborg moves those patterns to compile time.

For the runtime architecture those generators plug into, see [Cyborg Architecture](../architecture.md).

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Design Role](#design-role)
- [Contract Registration Bootstrap](#contract-registration-bootstrap)
- [Module Loader Factory Generator](#module-loader-factory-generator)
- [Generated Module Validation](#generated-module-validation)
  - [Validation Pipeline Shape](#validation-pipeline-shape)
  - [Supported Validation and Rewrite Behaviors](#supported-validation-and-rewrite-behaviors)
- [Generated Decomposition](#generated-decomposition)
- [Why the Generator Layer Exists](#why-the-generator-layer-exists)

<!-- /code_chunk_output -->

---

## Design Role

`Cyborg.Core.Aot` is a Roslyn generator assembly consumed by the runtime and module projects as an analyzer. Its generators produce the repetitive code required to make Cyborg's configuration and module model practical without resorting to runtime reflection.

In concrete terms, the generator layer currently covers four concerns:

- discovering runtime contracts needed by generators,
- generating module-loader factory methods,
- generating module validation, defaulting, and override-resolution code,
- generating `IDecomposable` implementations.

## Contract Registration Bootstrap

The generators need to emit code that references runtime types defined outside the generator assembly, such as `IModuleRuntime`, `ValidationResult<T>`, `DynamicKeyValuePair`, and `IDecomposable`.

Because the generator assembly targets the Roslyn generator environment and should not hardcode brittle type-name assumptions, Cyborg uses a contract-registration pattern:

1. the bootstrap generator emits the contract enums and `[GeneratorContractRegistration<TContract>]` attribute into the consuming compilation,
2. runtime types register themselves against those contract enums,
3. `ContractExplorer` discovers the registrations during generation,
4. the concrete generators use the discovered symbols when rendering source.

This makes the generator layer resilient to normal refactoring of runtime types while still failing fast when a required contract is missing.

## Module Loader Factory Generator

The module-loader factory generator removes the need to hand-write worker-construction boilerplate.

A module loader already knows:

- the module record type,
- the worker type,
- the shared `IServiceProvider`.

The missing piece is the worker factory method that passes the module instance directly and resolves the remaining constructor parameters from dependency injection. The generator emits that method.

Architecturally, this matters because Cyborg avoids reflection-driven activation in the module path. Worker creation stays explicit, trim-safe, and reviewable while module authors do not need to repeat the same `GetRequiredService<T>()` code for every loader.

## Generated Module Validation

The generated validation layer is the most important generator output in the system. It turns annotated record types into executable module contracts that participate in the runtime lifecycle.

Modules marked with `[GeneratedModuleValidation]` receive generated implementations that satisfy the runtime's expectations for typed validation and rewriting.

### Validation Pipeline Shape

The generated `ValidateAsync` method follows this shape:

1. apply defaults,
2. resolve overrides from the runtime environment,
3. apply defaults again,
4. validate the final object graph,
5. return `ValidationResult<TModule>`.

The second default pass is intentional. It ensures that values injected through overrides still flow through the defaulting logic of nested records and collections.

The generator also emits the dedicated helper stages used by that pipeline:

- `ApplyDefaultsAsync`
- `ResolveOverridesAsync`
- `ValidateAsync`

Validation walks nested validatable records and collection elements recursively and accumulates all discovered validation errors before returning.

### Supported Validation and Rewrite Behaviors

The validation generator understands the framework's validation and rewrite attributes, including:

- required-value checks,
- enum-value checks,
- min/max/length/range checks,
- regex and grammar checks,
- file and directory existence checks,
- default values, default instances, and default factories,
- override suppression through `IgnoreOverrides`.

The generator therefore handles both pure validation and runtime rewriting. A module record is not just checked; it is normalized into the final executable instance that the worker receives.

## Generated Decomposition

Records marked with `[GeneratedDecomposition]` receive an `IDecomposable` implementation that returns their public decomposable properties as `DynamicKeyValuePair` entries.

This is the mechanism behind structured environment publication and artifact flattening. It is what allows a result object or configuration value to be published once and then accessed through hierarchical variable paths such as:

- `host`
- `host.hostname`
- `host.port`

The generator intentionally keeps the output simple: it emits a property-to-key projection using the configured naming policy. The runtime then decides how far to recurse through that object graph based on the selected decomposition strategy.

## Why the Generator Layer Exists

Without the generator layer, Cyborg would have to choose between two poor options:

- heavy runtime reflection, which conflicts with native AOT and trimming, or
- large amounts of repetitive handwritten code, which is hard to review and easy to make inconsistent.

The source-generator layer avoids both problems.

It keeps:

- the public configuration model declarative,
- the runtime implementation explicit,
- the compiled binary compatible with native AOT expectations,
- module authoring predictable across the codebase.
