# Source Generators

This document describes the Roslyn source generators in `Cyborg.Core.Aot`. The generator layer produces the compile-time code that makes the module system, validation pipeline, and decomposition model work without runtime reflection, enabling native AOT compilation and trim safety.

For the runtime architecture these generators integrate with, see [Architecture Overview](architecture-overview.md). For a complete reference of all supported attributes, see [Validation Attributes Reference](validation-attributes-reference.md).

**Table of Contents**

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Design Role](#design-role)
- [Contract Discovery](#contract-discovery)
  - [Contract Types](#contract-types)
  - [Discovery Mechanism](#discovery-mechanism)
- [Module Validation Generator](#module-validation-generator)
  - [Trigger and Target](#trigger-and-target)
  - [Generated Pipeline](#generated-pipeline)
  - [Processor Architecture](#processor-architecture)
  - [Supported Attributes](#supported-attributes)
  - [Rendering Pipeline](#rendering-pipeline)
  - [Nested and Collection Handling](#nested-and-collection-handling)
- [Module Loader Factory Generator](#module-loader-factory-generator)
  - [Trigger and Target](#trigger-and-target-1)
  - [Generated Output](#generated-output)
  - [Constructor Resolution](#constructor-resolution)
- [Model Decomposition Generator](#model-decomposition-generator)
  - [Trigger and Target](#trigger-and-target-2)
  - [Generated Output](#generated-output-1)
  - [Naming Policy](#naming-policy)
- [Common Architecture](#common-architecture)
  - [Incremental Generation](#incremental-generation)
  - [Rendering Infrastructure](#rendering-infrastructure)
  - [Type Reference Safety](#type-reference-safety)
  - [Diagnostics](#diagnostics)

<!-- /code_chunk_output -->

## Design Role

`Cyborg.Core.Aot` is a Roslyn incremental generator assembly consumed by the module projects as an analyzer reference. It targets netstandard2.0 as required by the Roslyn analyzer hosting model. The generators produce the repetitive, reflection-equivalent code that would otherwise need to be written by hand for every module type.

The generator layer produces four primary kinds of compile-time behavior:

- **Module preparation and validation** — Generating defaults, override resolution, interpolation, constraint validation, and the `IValidationResult<TModule>` contract from annotated module records.
- **Module descriptions** — Generating short module identity and asynchronous `IModuleDescriptor` traversal from the same property model used for validation. The format-neutral description tree can then be serialized by the debugger or other clients without per-module reflection. See [Workflow Debugging](debugging.md).
- **Module loader factories** — Generating worker construction methods that resolve constructor dependencies from the DI container, eliminating boilerplate in module loaders.
- **Model decomposition** — Generating `IDecomposable` implementations that project record properties into `DynamicKeyValuePair` collections for environment publishing and artifact flattening.

A contract-registration bootstrap supports these feature generators by establishing the compile-time type-discovery mechanism that decouples generator code from runtime assemblies.

## Contract Discovery

The generators need to emit code referencing runtime types defined in other assemblies — `IModuleRuntime`, `IValidationResult<T>`, `ValidationResult`, `DynamicKeyValuePair`, and others. Because the generator assembly cannot directly reference those types (it targets netstandard2.0 and runs in the Roslyn analyzer host), Cyborg uses a contract registration pattern to discover them at generation time.

### Contract Types

Each generator declares a contract enum whose members correspond to the runtime types it requires. Three contracts exist:

| Contract | Members | Used By |
|----------|---------|---------|
| `ModuleValidationGeneratorContract` | `IModuleRuntime`, `IModuleT`, `ModuleValidationContext`, `ValidationResult`, `IValidationResultT`, `ValidationError`, `IDefaultValueT`, `IParser`, `IModuleDescriptor`, `IObjectDescriptionBuilder`, `ModuleIdentity` | Validation and descriptor generation |
| `ModuleLoaderFactoryGeneratorContract` | `IModuleWorker`, `ModuleLoaderT`, `IModuleWorkerContextT`, `ModuleWorkerContextImplementationT` | Loader factory generator |
| `ModelDecompositionGeneratorContract` | `IDecomposable`, `DynamicKeyValuePair` | Decomposition generator |

Runtime types register themselves against these contracts using `[GeneratorContractRegistration<TContract>(ContractMember)]`. A single contract may include both runtime execution types and description types when one generator emits code that participates in both subsystems.

### Discovery Mechanism

The `ContractExplorer` scans all assemblies referenced by the compilation, enumerates their types, and collects `GeneratorContractRegistration` attributes. For each attribute, it extracts the contract enum value from the constructor argument and maps it to the annotated type symbol. The result is a dictionary from contract member to resolved type symbol, which the generator then uses when emitting code.

The bootstrap generator (`ContractRegistrationBootstrapGenerator`) emits the contract enums and the registration attribute type into the consuming compilation via `RegisterPostInitializationOutput`, making them available for the runtime assemblies to apply.

If a required contract registration is missing, the generator reports a `CYBORG001` diagnostic error. Duplicate registrations produce `CYBORG002`.

## Module Validation Generator

The validation generator is the most substantial generator in the system. It turns annotated module records into executable validation pipelines that participate in the runtime module lifecycle.

### Trigger and Target

The generator is triggered by the `[GeneratedModuleValidation]` attribute on a `partial record` type. The target must be a record (to support `with`-expression immutability) and must be declared `partial` so the generator can emit the implementing methods.

### Generated Pipeline

For each annotated record, the generator emits a partial record implementing `IModule<TModule>` and `IModuleDescriptor`. The validation pipeline consists of one public async validation method and three private async instance helpers:

1. **`ApplyDefaultsAsync`** — Applies declared defaults and property-level preparation invariants through generated `with`-expressions. Default attributes (`[DefaultValue<T>]`, `[DefaultInstance]`, `[DefaultInstanceFactory]`, `[DefaultTimeSpan]`) replace null or zero-valued properties, while aspects such as `[Secret]` rewrite the effective value to re-establish destination metadata. The pass recurses into nested records marked `[Validatable]` and supported collection elements.

2. **`ResolveOverridesAsync`** — For each property not suppressed by `[IgnoreOverride]`, emits an operation through `ModuleValidationContext`. `string` and `TaggedString` properties use raw override selection so `[IgnoreInterpolation]` can preserve the effective expression and tagged values retain their metadata; non-text properties and collections use typed resolution. `[IgnoreOverride]` suppresses the annotated node; `Recurse = true` also suppresses descendants. The preparation pass runs again after this phase so injected type-default values receive declared defaults and destination invariants are re-established.

3. **`ApplyInterpolationAsync`** — Private instance helper that recursively rewrites eligible string and `TaggedString` properties through `ModuleValidationContext.Interpolate(...)`, including values in nested `[Validatable]` records and supported collections. Tags union across interpolated operands. `[IgnoreInterpolation]` leaves a value untouched for later context-specific interpolation. `[Untagged]` suppresses the diagnostic that recommends migrating remaining string properties to `TaggedString`.

4. **`ValidateAsync`** — Creates one `ModuleValidationContext` from the runtime and service provider, orchestrates defaults → overrides → defaults → interpolation → constraints, collects `ValidationError` instances, and returns `IValidationResult<TModule>` through the shared `ValidationResult.Valid(...)` / `Invalid(...)` factories. Invalid generated results retain the fully prepared module so lifecycle hooks, debugger inspection, and diagnostics can observe the same state that would otherwise reach validation enforcement. Validation recurses into nested validatable records and supported collection elements.

The generated code uses `with`-expressions throughout, ensuring that each stage produces a new record instance and that the original deserialized module is never mutated.

`ModuleValidationContext` is registered as a generator contract because the generated helpers are compiled into the consuming module assembly. The type is public at the CLR level for that cross-assembly call path, but it lives in an `Internal` namespace, is hidden from IntelliSense, and exposes the internal override primitives only to generated preparation code. `IModule<TModule>` itself requires only `ValidateAsync(...)`; the three preparation helpers remain private implementation details of the generated partial record.

### Processor Architecture

The validation generator does not hardcode attribute handling. Instead, it uses a processor registry pattern where each validation or defaulting behavior is encapsulated in a processor class.

Two processor interfaces exist:

- **`IPropertyAttributeProcessor`** — Triggered when its `AttributeMetadataName` matches an attribute on the property being processed. Handles attribute-driven behaviors like `[Required]`, `[Range<T>]`, and `[DefaultValue<T>]`.
- **`IDynamicPropertyProcessor`** — Invoked for every property regardless of attributes. Handles context-dependent behaviors such as collection override resolution, where the processing logic depends on the property type rather than an attribute.

Each processor returns a `PropertyAspect` — an object that can contribute to one or more pipeline stages. An aspect can contribute default expressions, rewrite the prepared property value, rewrite override resolution, add descriptor hints, and emit validation code. This allows one attribute to establish behavior across preparation and validation without duplicating traversal logic. `[DefaultValue<T>]` contributes only default selection, while `[Secret]` adds an intrinsic tag during preparation, contributes the same tag as an inspection hint, and asserts the invariant during final validation.

Validation attributes that support collection elements derive from `PropertyValidationAttribute` and are processed through `PropertyValidationProcessorBase<TAttribute>`. The base processor resolves the optional `TargetsElements` flag, verifies that the property is a supported collection when element targeting is requested, and evaluates attribute-specific type requirements against either the property type or the collection element type. Element-targeted constraints are wrapped in a `CollectionElementValidationAspect`, allowing the individual attribute processors to emit the same validation logic for either target without implementing collection traversal themselves. Because these attributes are repeatable, a property can contribute both an ordinary property aspect and one or more element-targeted aspects.

The `ValidationProcessorRegistry` holds the complete set of processors as a static immutable array, with a frozen dictionary for attribute-based lookup by metadata name.

### Supported Attributes

The following attributes are recognized by the validation generator:

| Category | Attributes |
|----------|-----------|
| **Required values** | `[Required]` |
| **Default values** | `[DefaultValue<T>]`, `[DefaultInstance]`, `[DefaultInstanceFactory]`, `[DefaultTimeSpan]` |
| **Length constraints** | `[MinLength]`, `[MaxLength]`, `[ExactLength]`, `[Length]` |
| **Variable syntax** | `[VariableIdentifier]` |
| **Range constraints** | `[Range<T>]` |
| **Pattern matching** | `[MatchesRegex]`, `[MatchesGrammar]` |
| **File system and paths** | `[FileExists]`, `[DirectoryExists]`, `[FileName]`, `[RootedPath]`, `[UnrootedPath]`, `[NormalizedPath]` |
| **Enum validation** | `[DefinedEnumValue]` |
| **Override suppression** | `[IgnoreOverride]` |
| **Interpolation suppression** | `[IgnoreInterpolation]` |
| **Tagged strings** | `[Secret]`, `[Untagged]` |
| **Nested validation** | `[Validatable]` (on nested record types) |

All attributes are defined in `Cyborg.Core.Aot` and emitted into the consuming compilation, see [Validation Attributes Reference](validation-attributes-reference.md) for a complete reference of their parameters and behavior.

### Rendering Pipeline

The validation generator renders one partial module declaration from a shared property model. Its generated behavior is organized into four preparation/validation stages plus module description output:

| Generated member | Responsibility |
|------------------|----------------|
| `ApplyDefaultsAsync` | Apply declared defaults and preparation invariants recursively |
| `ResolveOverridesAsync` | Resolve eligible runtime overrides recursively |
| `ApplyInterpolationAsync` | Interpolate eligible strings recursively |
| `ValidateAsync` | Orchestrate preparation and emit constraint checks |
| `GetDescriptor` / `DescribeAsync` | Expose format-neutral identity and structural description |

The description traversal uses the same recursive property graph and collection guards as the preparation pipeline, so validation and inspection agree on which values are scalar, nested, absent, or enumerable. Property aspects may also contribute arbitrary descriptor-hint keys. Hints are emitted as metadata and remain serializer-neutral; interpretation belongs to description consumers rather than the generator.

### Nested and Collection Handling

The generator supports recursive validation of nested record types and collection elements:

- **Nested records** — Properties whose type is marked `[Validatable]` are processed recursively. The generator detects cycles in the type graph to prevent infinite recursion during generation.
- **Collections** — Properties typed as supported enumerable shapes are rewritten and validated element-by-element when their element type requires work. `CollectionTypeInspector` selects a `CollectionMaterializationKind` for arrays, `List<T>`, `ImmutableArray<T>`, supported collection interfaces, and constructible concrete collections; `string` is explicitly excluded and remains a scalar despite implementing `IEnumerable<char>`. Shared enumeration guards preserve collection absence semantics across validation, interpolation, defaults, and description generation: null references are skipped, nullable value types are unwrapped only when present, and default `ImmutableArray<T>` values are never enumerated or silently converted to empty arrays.
- **Element-targeted constraints** — Selected validation attributes can set `TargetsElements = true` to apply their constraint to each immediate collection element. The same guarded loop is shared with recursive validation of `[Validatable]` element records, while ordinary property constraints remain outside the guard. This allows repeated attributes to constrain the collection and its elements independently. Attribute-specific target checks use the element type, and element validation errors retain the parent property name while identifying the zero-based element index in the message.

## Module Loader Factory Generator

### Trigger and Target

The generator is triggered by `[GeneratedModuleLoaderFactory]` on a class inheriting from `ModuleLoader<TWorker, TModule>`. The target class must be `partial`. The worker type must have exactly one declared constructor.

### Generated Output

The generator emits a `CreateWorker` method (or a custom-named method if specified in the attribute) that constructs the worker type by resolving its constructor parameters:

- Parameters whose type matches the module type receive the `module` argument directly.
- Parameters whose type matches `IModuleWorkerContext<TModule>` are constructed inline, with their own constructor parameters resolved recursively.
- All other parameters are resolved via `serviceProvider.GetRequiredService<T>()`.

This eliminates the boilerplate of manually writing service resolution code for every module loader while keeping worker construction explicit and trim-safe.

### Constructor Resolution

The generator inspects the worker type's single constructor at compile time, determines the resolution strategy for each parameter, and emits the corresponding constructor call. If the worker type has zero or more than one declared constructor, or if the target class does not inherit from the expected base type, the generator reports a diagnostic error.

## Model Decomposition Generator

### Trigger and Target

The generator is triggered by `[GeneratedDecomposition]` on a `partial record` or `partial class`. It emits an `IDecomposable` implementation.

### Generated Output

The generated `Decompose()` method returns a collection of `DynamicKeyValuePair` entries, one per public instance property visible on the annotated type, including inherited properties. Static properties and properties marked with `[DecomposeIgnore]` are excluded. If a derived type hides or overrides a property name, the most-derived property is emitted once. Each entry pairs a transformed property name (as the key) with the property value.

### Naming Policy

Property names are transformed using a configurable naming policy. The attribute accepts two optional parameters:

- `NamingPolicyProvider` — The type containing the naming policy (defaults to `JsonNamingPolicy`).
- `NamingPolicy` — The static property name on that type (defaults to `"SnakeCaseLower"`).

The generated code calls the naming policy's `ConvertName` method on each property name at runtime, producing keys that match the JSON serialization convention (typically snake_case).

## Common Architecture

### Incremental Generation

All generators implement `IIncrementalGenerator` and follow the Roslyn incremental generation model. Each generator registers a syntax provider that filters for the relevant attribute, transforms syntax nodes into generation candidates, and combines them with contract discovery results before emitting source. This ensures generation work is cached and only re-executed when the relevant source changes.

### Rendering Infrastructure

All generators render source through shared indentation and type-rendering utilities so emitted code follows consistent formatting and fully qualified type-reference rules. The validation generator keeps preparation, validation, and description stages separate while deriving them from one analyzed property graph.

Accessibility decisions are evaluated relative to the lexical context of the generated partial module rather than the value type of a recursively processed property. This is important for nested and inherited validatable properties: generated member access must reflect what code emitted inside the root partial type can actually read or assign. Loader-factory and decomposition output follow the same general rendering conventions without sharing the validation property model.

### Type Reference Safety

Generated code references runtime types using fully qualified global names (e.g., `global::System.IServiceProvider`) defined in the `KnownTypes` static class. This avoids namespace conflicts and ensures generated code compiles correctly regardless of the consuming project's `using` directives.

For runtime types discovered through the contract system, generators use the resolved type symbols from `ContractExplorer`, rendering them with their global namespace prefix via extension methods on `INamedTypeSymbol`.

### Diagnostics

Each generator defines its own set of diagnostic descriptors with unique IDs:

| Prefix | Generator | Examples |
|--------|-----------|----------|
| `CYBORG` | Contract bootstrap | Missing or duplicate contract registrations |
| `CYBORGMLF` | Loader factory | Invalid base type, missing constructor, incorrect method signature |
| `CYBORGCOMP` | Decomposition | Non-partial type, invalid naming policy configuration |
| `CYBORGVAL` | Validation | Non-partial record, invalid attribute usage, prefer `TaggedString` (`CYBORGVAL025`), `[Secret]`/`[Untagged]` misuse (`CYBORGVAL026`–`CYBORGVAL028`) |

Diagnostics are reported through `DiagnosticsReporter` (validation) or directly via the source production context. All diagnostics include the source location of the triggering declaration.
