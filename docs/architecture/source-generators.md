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

The generator layer covers three concerns:

- **Module validation** — Generating the three-stage validation pipeline (`ApplyDefaultsAsync`, `ResolveOverridesAsync`, `ValidateAsync`) from annotated module records, transforming declarative attributes into executable validation, defaulting, and override resolution logic.
- **Module loader factories** — Generating worker construction methods that resolve constructor dependencies from the DI container, eliminating boilerplate in module loaders.
- **Model decomposition** — Generating `IDecomposable` implementations that project record properties into `DynamicKeyValuePair` collections for environment publishing and artifact flattening.

A fourth generator, the contract registration bootstrap, supports the other three by establishing the type discovery mechanism that decouples generators from the runtime type system.

## Contract Discovery

The generators need to emit code referencing runtime types defined in other assemblies — `IModuleRuntime`, `ValidationResult<T>`, `DynamicKeyValuePair`, and others. Because the generator assembly cannot directly reference those types (it targets netstandard2.0 and runs in the Roslyn analyzer host), Cyborg uses a contract registration pattern to discover them at generation time.

### Contract Types

Each generator declares a contract enum whose members correspond to the runtime types it requires. Three contracts exist:

| Contract | Members | Used By |
|----------|---------|---------|
| `ModuleValidationGeneratorContract` | `IModuleRuntime`, `IModuleT`, `ValidationResultT`, `ValidationError`, `IDefaultValueT`, `IParser` | Validation generator |
| `ModuleLoaderFactoryGeneratorContract` | `IModuleWorker`, `ModuleLoaderT`, `IModuleWorkerContextT`, `ModuleWorkerContextImplementationT` | Loader factory generator |
| `ModelDecompositionGeneratorContract` | `IDecomposable`, `DynamicKeyValuePair` | Decomposition generator |

Runtime types register themselves against these contracts using `[GeneratorContractRegistration<TContract>(ContractMember)]`. For example, the `ValidationResult<T>` type in `Cyborg.Core` carries an attribute registering it as `ModuleValidationGeneratorContract.ValidationResultT`.

### Discovery Mechanism

The `ContractExplorer` scans all assemblies referenced by the compilation, enumerates their types, and collects `GeneratorContractRegistration` attributes. For each attribute, it extracts the contract enum value from the constructor argument and maps it to the annotated type symbol. The result is a dictionary from contract member to resolved type symbol, which the generator then uses when emitting code.

The bootstrap generator (`ContractRegistrationBootstrapGenerator`) emits the contract enums and the registration attribute type into the consuming compilation via `RegisterPostInitializationOutput`, making them available for the runtime assemblies to apply.

If a required contract registration is missing, the generator reports a `CYBORG001` diagnostic error. Duplicate registrations produce `CYBORG002`.

## Module Validation Generator

The validation generator is the most substantial generator in the system. It turns annotated module records into executable validation pipelines that participate in the runtime module lifecycle.

### Trigger and Target

The generator is triggered by the `[GeneratedModuleValidation]` attribute on a `partial record` type. The target must be a record (to support `with`-expression immutability) and must be declared `partial` so the generator can emit the implementing methods.

### Generated Pipeline

For each annotated record, the generator emits a partial record implementing `IModule<TModule>` with three async methods:

1. **`ApplyDefaultsAsync`** — For each property carrying a default attribute (`[DefaultValue<T>]`, `[DefaultInstance]`, `[DefaultInstanceFactory]`, `[DefaultTimeSpan]`), emits a `with`-expression that replaces null or zero-valued properties with their declared defaults. Recursively applies defaults to nested records marked `[Validatable]` and to elements within collections.

2. **`ResolveOverridesAsync`** — For each property not marked `[IgnoreOverrides]`, emits a call to `runtime.Environment.Resolve<TModule, T>()` to check for environment-driven overrides. The override resolution system (described in the architecture overview) uses `CallerArgumentExpression` to derive the property name, constructs lookup keys from the module's identifiers, and applies type conversion. Processors can customize the override resolution expression for specific property types (e.g., collections).

3. **`ValidateAsync`** — For each property carrying a validation attribute, emits constraint checks and collects `ValidationError` instances. Returns `ValidationResult<TModule>.Valid(module)` if no errors were found, or `ValidationResult<TModule>.Invalid(module, errors)` otherwise. Validation recurses into nested validatable records and iterates collection elements.

The generated code uses `with`-expressions throughout, ensuring that each stage produces a new record instance and that the original deserialized module is never mutated.

### Processor Architecture

The validation generator does not hardcode attribute handling. Instead, it uses a processor registry pattern where each validation or defaulting behavior is encapsulated in a processor class.

Two processor interfaces exist:

- **`IPropertyAttributeProcessor`** — Triggered when its `AttributeMetadataName` matches an attribute on the property being processed. Handles attribute-driven behaviors like `[Required]`, `[Range<T>]`, and `[DefaultValue<T>]`.
- **`IDynamicPropertyProcessor`** — Invoked for every property regardless of attributes. Handles context-dependent behaviors such as collection override resolution, where the processing logic depends on the property type rather than an attribute.

Each processor returns a `PropertyValidationAspect` — an object that can contribute to one or more pipeline stages. An aspect exposes virtual methods for rewriting the default assignment expression, rewriting the override resolution expression, and emitting validation code. This design allows a single attribute to influence multiple stages of the pipeline. For example, a `[DefaultValue<T>]` attribute produces an aspect that contributes a default in the defaults stage but contributes nothing to validation.

The `ValidationProcessorRegistry` holds the complete set of processors as a static immutable array, with a frozen dictionary for attribute-based lookup by metadata name.

### Supported Attributes

The following attributes are recognized by the validation generator:

| Category | Attributes |
|----------|-----------|
| **Required values** | `[Required]` |
| **Default values** | `[DefaultValue<T>]`, `[DefaultInstance]`, `[DefaultInstanceFactory]`, `[DefaultTimeSpan]` |
| **Length constraints** | `[MinLength]`, `[MaxLength]`, `[ExactLength]`, `[Length]` |
| **Range constraints** | `[Range<T>]` |
| **Pattern matching** | `[MatchesRegex]`, `[MatchesGrammar]` |
| **File system** | `[FileExists]`, `[DirectoryExists]` |
| **Enum validation** | `[DefinedEnumValue]` |
| **Override suppression** | `[IgnoreOverrides]` |
| **Nested validation** | `[Validatable]` (on nested record types) |

All attributes are defined in `Cyborg.Core.Aot` and emitted into the consuming compilation, see [Validation Attributes Reference](validation-attributes-reference.md) for a complete reference of their parameters and behavior.

### Rendering Pipeline

The generator assembles its output through three section renderers, each implementing `ISectionRenderer`:

| Renderer | Method Generated | Responsibility |
|----------|-----------------|----------------|
| `DefaultsSectionRenderer` | `ApplyDefaultsAsync` | Emits default value assignments from aspect rewrite expressions |
| `OverrideSectionRenderer` | `ResolveOverridesAsync` | Emits override resolution calls with aspect rewrite expressions |
| `ValidationSectionRenderer` | `ValidateAsync` | Emits constraint checks from aspect validation logic |

The `ModuleValidationRenderer` orchestrates these renderers sequentially into a single partial record declaration. It also emits file-scoped helper methods for default instance resolution and nullable relaxation.

### Nested and Collection Handling

The generator supports recursive validation of nested record types and collection elements:

- **Nested records** — Properties whose type is marked `[Validatable]` are processed recursively. The generator detects cycles in the type graph to prevent infinite recursion during generation.
- **Collections** — Properties typed as `IReadOnlyCollection<T>`, `ImmutableArray<T>`, `List<T>`, or arrays are iterated element-by-element during validation. If the element type is itself validatable, each element is validated recursively. Collection materialization (converting between collection types after override resolution) is handled via `CollectionTypeInspector` and `CollectionMaterializationKind`.

## Module Loader Factory Generator

### Trigger and Target

The generator is triggered by `[GeneratedModuleLoaderFactory]` on a class inheriting `ModuleLoader<TWorker, TModule>`. The target class must be `partial`. The worker type must have exactly one declared constructor.

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

The generated `Decompose()` method returns a collection of `DynamicKeyValuePair` entries, one per public property. Properties marked with `[DecomposeIgnore]` are excluded. Each entry pairs a transformed property name (as the key) with the property value.

### Naming Policy

Property names are transformed using a configurable naming policy. The attribute accepts two optional parameters:

- `NamingPolicyProvider` — The type containing the naming policy (defaults to `JsonNamingPolicy`).
- `NamingPolicy` — The static property name on that type (defaults to `"SnakeCaseLower"`).

The generated code calls the naming policy's `ConvertName` method on each property name at runtime, producing keys that match the JSON serialization convention (typically snake_case).

## Common Architecture

### Incremental Generation

All generators implement `IIncrementalGenerator` and follow the Roslyn incremental generation model. Each generator registers a syntax provider that filters for the relevant attribute, transforms syntax nodes into generation candidates, and combines them with contract discovery results before emitting source. This ensures generation work is cached and only re-executed when the relevant source changes.

### Rendering Infrastructure

All generators render source code using `IndentedStringBuilder`, a custom builder that manages indentation levels via `IncreaseIndent()` and `DecreaseIndent()`. This produces consistently formatted generated code regardless of nesting depth.

The validation generator further decomposes rendering into `ISectionRenderer` implementations, allowing each pipeline stage to be rendered independently. The loader factory and decomposition generators use dedicated renderer classes (`LoaderFactoryRenderer`, `ModelDecompositionRenderer`) with static `Render` methods.

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
| (validation) | Validation | Non-partial record, invalid attribute usage |

Diagnostics are reported through `DiagnosticsReporter` (validation) or directly via the source production context. All diagnostics include the source location of the triggering declaration.
