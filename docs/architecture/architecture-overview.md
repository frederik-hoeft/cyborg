# Cyborg System Architecture

This document provides a comprehensive overview of the Cyborg system architecture. It covers the module system, JSON deserialization, execution model, environment scoping, variable resolution, property overrides, artifact publishing, parsing infrastructure, process execution, metrics, and security. After reading this document, you should have a clear understanding of how the system is structured, how modules are loaded and executed, and how the major subsystems interact.

For detailed reference material, see:

- [Module Reference](modules-reference.md) — Complete documentation of all built-in modules
- [Dynamic Values Reference](dynamic-values-reference.md) — Dynamic value providers and typed configuration
- [Templates Reference](templates-reference.md) — Template module usage and patterns
- [Source Generators](source-generators.md) — Roslyn source generators for AOT-compatible code generation
- [Validation Attributes Reference](validation-attributes-reference.md) — Validation, defaulting, override, and interpolation control attributes
- [Module Testing](module-testing.md) — Production-backed test infrastructure and generator regression fixtures
- [Workflow Debugging](debugging.md) — Breakpoints, interactive REPL, module inspection, and debug adapters

**Table of Contents**

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Overview](#overview)
- [Project Structure](#project-structure)
  - [Test Support Projects](#test-support-projects)
- [Module System](#module-system)
  - [Three-Part Module Pattern](#three-part-module-pattern)
  - [ModuleContext Envelope](#modulecontext-envelope)
  - [Module Composition via ModuleReference](#module-composition-via-modulereference)
  - [Loading and Deserialization](#loading-and-deserialization)
    - [Registry-Based Deserialization](#registry-based-deserialization)
    - [Dynamic Value System](#dynamic-value-system)
- [Module Execution](#module-execution)
  - [Execution Lifecycle](#execution-lifecycle)
    - [Validation Pipeline](#validation-pipeline)
    - [Execution and Result](#execution-and-result)
  - [Runtime Hierarchy](#runtime-hierarchy)
  - [Environment Binding](#environment-binding)
- [Runtime Environment](#runtime-environment)
  - [Environment Scoping](#environment-scoping)
    - [Scope Types](#scope-types)
    - [Environment Types](#environment-types)
    - [Named Environments](#named-environments)
  - [Variable Resolution](#variable-resolution)
    - [Resolution Semantics](#resolution-semantics)
    - [Cycle Detection](#cycle-detection)
    - [Variable Name Syntax](#variable-name-syntax)
    - [Decomposable Objects](#decomposable-objects)
  - [Module Property Overrides](#module-property-overrides)
    - [Override Resolution](#override-resolution)
    - [Override Use Case](#override-use-case)
    - [Override Resolution Tags](#override-resolution-tags)
  - [Artifact Publishing](#artifact-publishing)
    - [Artifact Lifecycle](#artifact-lifecycle)
    - [Artifact Configuration](#artifact-configuration)
    - [Artifact Exposure Patterns](#artifact-exposure-patterns)
- [Supporting Infrastructure](#supporting-infrastructure)
  - [Parsing Infrastructure](#parsing-infrastructure)
    - [Parser Combinators](#parser-combinators)
    - [Terminal Parsers](#terminal-parsers)
    - [Syntax Tree and Data Extraction](#syntax-tree-and-data-extraction)
    - [Integration Points](#integration-points)
  - [Process Execution](#process-execution)
  - [Metrics Collection](#metrics-collection)
- [Cross-Cutting Concerns](#cross-cutting-concerns)
  - [Security Design Principles](#security-design-principles)
    - [Configuration File Trust](#configuration-file-trust)
    - [Subprocess Safety](#subprocess-safety)
    - [Input Validation](#input-validation)
    - [Privilege Boundaries](#privilege-boundaries)
  - [AOT Compilation](#aot-compilation)

<!-- /code_chunk_output -->


## Overview

Cyborg is a .NET 10 application providing modular, JSON-configured backup orchestration with native AOT compilation support. It replaces legacy shell-based backup scripts with a type-safe, extensible module system. The architecture is driven by four design goals:

1. **AOT Compilation** — Native AOT publishing for minimal startup time and memory footprint on Linux servers, and minimal external dependencies (no .NET runtime requirement, no external dynamic libraries).
2. **Extensibility** — A plugin-like module system allowing backup operations to be composed from JSON configuration without code changes.
3. **Type Safety** — Compile-time verification of module registration, dependency injection, and JSON serialization through Roslyn source generators and the Jab DI container.
4. **Structured Output Parsing** — Grammar-based parser combinators for extracting structured data and metrics from subprocess output.

## Project Structure

The production solution is organized into six primary projects, each with a specific role in the dependency hierarchy:

| Layer | Target | Purpose |
|-------|--------|---------|
| `Cyborg.Core` | net10.0 | Core abstractions: module interfaces, runtime, environment scoping, configuration, parsing, and cross-cutting services. Contains no module-specific logic. |
| `Cyborg.Core.Aot` | netstandard2.0 | Roslyn incremental source generators distributed as analyzers. Targets netstandard2.0 as required by the Roslyn analyzer hosting model. |
| `Cyborg.Modules` | net10.0 | Built-in, domain-agnostic module implementations supplemented by generated code from `Cyborg.Core.Aot`, e.g., for model validation and instance activation. |
| `Cyborg.Modules.Borg` | net10.0 | Borg-specific modules (create, prune, compact) with JSON output parsing and borg-specific configuration types. |
| `Cyborg.Cli.Debugging` | net10.0 | Console debugger frontend and its isolated ConsoleAppFramework-generated REPL command surface. |
| `Cyborg.Cli` | net10.0 | Application entry point using its own ConsoleAppFramework command surface, with Jab for compile-time dependency injection composition. |

`Cyborg.Core` defines the runtime interfaces and abstractions. `Cyborg.Core.Aot` generates code that implements those interfaces for specific module types. `Cyborg.Modules` and `Cyborg.Modules.Borg` provide the built-in module library. `Cyborg.Cli.Debugging` provides the console debugger adapter in a separate CAF compilation, and `Cyborg.Cli` composes everything into the final executable. Each module library exposes a Jab `[ServiceProviderModule]` interface (e.g., `ICyborgModuleServices`, `ICyborgBorgServices`) that the CLI project imports into its composition root.

### Test Support Projects

The reusable test infrastructure is split by responsibility:

| Project | Purpose |
|---------|---------|
| `Cyborg.Core.TestAdapter` | Builds production-equivalent runtime scopes for tests and exposes the higher-order test API. |
| `Cyborg.TestModules` | Minimal non-test assembly containing source-generator-enrolled fixture models. It references `Cyborg.Core` and consumes `Cyborg.Core.Aot` as an analyzer. |
| `Cyborg.Core.Tests` | Unit tests for core runtime and infrastructure behavior. |
| `Cyborg.Modules.Tests` | Tests for domain-agnostic modules and generated validation behavior. References `Cyborg.TestModules` but does not itself consume the validation analyzer. |
| `Cyborg.Modules.Borg.Tests` | Tests for Borg-specific modules, parsers, and metrics behavior. |

Keeping generated fixture models in `Cyborg.TestModules` avoids exposing the analyzer-generated framework types directly inside friend test assemblies. `Cyborg.Modules.Tests` is an `InternalsVisibleTo` target of `Cyborg.Core`; enrolling that same compilation for source generation would make both the internal framework types from `Cyborg.Core` and newly emitted copies visible, creating conflicting type definitions.

## Module System

The module system is the central architectural pattern in Cyborg. Every unit of work — from executing a subprocess to orchestrating a multi-step backup workflow — is represented as a module.

### Three-Part Module Pattern

Each module consists of three types serving distinct responsibilities:

| Type | Responsibility | Lifetime |
|------|----------------|----------|
| Module (record) | Immutable configuration data holder. Pure data, safe to cache or transform. Inherits from `ModuleBase` and implements `IModule`. | Per-configuration |
| Worker | Execution logic. Inherits from `ModuleWorker<TModule>`, receives injected services, and implements module behavior through the abstract `ExecuteAsync` method. | Per-configuration, stateless |
| Loader | JSON deserialization. Inherits from `ModuleLoader<TWorker, TModule>` with a source-generated factory method that constructs the worker from the deserialized module record and dependency-injected services. | Singleton |

Before execution, the immutable module record is copied and transformed through the generated preparation and validation pipeline, applying defaults, per-execution overrides, string interpolation, and constraints. The worker operates on the fully validated module instance, ensuring that execution logic never encounters invalid configuration and that deserialized module definitions remain immutable and free of execution-time side effects.

The separation of module from worker ensures that configuration data remains immutable and free of side effects. Workers are instantiated per configuration, receive the validated module record, and have access to dependency-injected services. Loaders are singletons registered in the module loader registry at startup.

### ModuleContext Envelope

Every module invocation in JSON is represented as a `ModuleContext` — an envelope that separates the module definition from its execution context:

| Field | Purpose |
|-------|---------|
| `module` | The module to execute, identified by its versioned module ID |
| `environment` | Scoping configuration for the execution environment (scope, name, transient flag, variable definitions) |
| `configuration` | Optional configuration module that populates the environment before the main module runs |
| `requires` | Optional pre-execution requirements that are asserted before the module executes |

When a `ModuleContext` is executed, the runtime first prepares the environment according to the declared scope, then runs the `configuration` module (if present) to populate variables, and finally executes the main `module` within that prepared environment.

### Module Composition via ModuleReference

The `ModuleReference` type enables modules to contain other modules as properties, creating arbitrarily nested execution trees. In JSON, a module reference is expressed as an object whose single property name is the module ID and whose value is the module's configuration. This structure eliminates the need for `$type` discriminators while enabling polymorphic, version-aware deserialization. Any module property typed as `ModuleReference` or `ModuleContext` can hold any module, enabling compositional patterns such as sequences of conditionals, loops over parameterized templates, or guards wrapping subprocess calls.

### Loading and Deserialization

Cyborg uses a dynamic, registry-based JSON deserialization model where the module ID serves as both a discriminator and a version identifier. This approach is fully AOT-compatible, avoiding reflection-based polymorphic deserialization.

#### Registry-Based Deserialization

When the JSON deserializer encounters a `ModuleReference`, the `ModuleReferenceJsonConverter` reads the property name as a module ID, looks up the corresponding `IModuleLoader` from the `IModuleLoaderRegistry`, and delegates deserialization to that loader. The registry is backed by a `FrozenDictionary` populated at startup from all registered `IModuleLoader` implementations. Each loader produces an `IModuleWorker` ready for execution. This registry-based dispatch enables version-aware loading and extensibility — new modules only need to register a loader at startup.

Module IDs follow a versioned, dot-separated naming convention (e.g., `cyborg.modules.subprocess.v1`). All JSON property names use `snake_case` via `JsonKnownNamingPolicy.SnakeCaseLower`.

#### Dynamic Value System

Configuration modules populate the environment with typed values using the `IDynamicValueProvider` subsystem. Each entry in a configuration map is a key-value pair where the value type is identified by a property name in the JSON object. Providers are registered by type name (e.g., `"int"`, `"string"`, `"bool"`) in the `IDynamicValueProviderRegistry`. Domain-specific types register under versioned type names (e.g., `"cyborg.types.borg.remote.v1.4"`). Typed collections use `collection<T>` syntax to declare arrays of a specific value type.

Custom types implement `IDynamicValueProvider` and register a versioned type name. When annotated with `[GeneratedDecomposition]`, they gain `IDecomposable` support for hierarchical property access via the variable resolution subsystem. Dynamic key-value entries must contain a non-null `key` and exactly one non-null typed value; malformed entries fail immediately with `JsonException`, including when they are parsed outside a module-validation context. See the [Dynamic Values Reference](dynamic-values-reference.md) for a complete listing of available value providers.

## Module Execution

This section describes how modules are executed at runtime: the validation pipeline that prepares module records before execution, the runtime hierarchy that manages nested module dispatch, and the environment binding model that determines each module's execution context.

### Execution Lifecycle

The `ModuleWorker<TModule>` base class orchestrates the complete lifecycle from raw configuration through validation to execution and artifact publishing. This lifecycle ensures that no worker ever operates on unvalidated configuration.

#### Validation Pipeline

Before a worker's `ExecuteAsync` method is invoked, `ModuleWorker<TModule>` calls the source-generated `ValidateAsync` implementation on the module. The generated method orchestrates five ordered phases:

1. **Apply Defaults** — Fills null or type-default properties from `[DefaultValue<T>]`, `[DefaultInstance]`, `[DefaultInstanceFactory]`, and `[DefaultTimeSpan]`. Defaulting recurses into nested records marked with `[Validatable]` and supported collection elements.

2. **Resolve Overrides** — Substitutes eligible module properties from runtime environment variables using the override subsystem described in [Module Property Overrides](#module-property-overrides). `[IgnoreOverride]` suppresses replacement of the annotated property; its optional `recurse` constructor argument also suppresses descendants when `true`.

3. **Reapply Defaults** — Runs defaulting again so values introduced by overrides receive the same default semantics as values produced by deserialization.

4. **Interpolate Strings** — Recursively applies `runtime.Environment.Interpolate(...)` to eligible strings on the module, nested `[Validatable]` records, and supported collection elements. `[IgnoreInterpolation]` preserves strings that require later context-specific interpolation. `ModuleBase.Name` and `ModuleBase.Group` opt out because they establish structural identity before validation; `AssertModule.Message` is interpolated by its worker after the assertion child has executed so it can reference child artifacts.

5. **Validate Constraints** — Checks constraints declared through `[Required]`, `[VariableIdentifier]`, `[Range<T>]`, length, filesystem, path-shape, regex, grammar, enum, and related validation attributes. Produces a `ValidationResult<TModule>` that retains the transformed module and carries any validation errors.

Each transformation uses `with` expressions, so the original deserialized module is never mutated. The generator emits private async `ApplyDefaultsAsync`, `ResolveOverridesAsync`, and `ApplyInterpolationAsync` instance helpers plus the public `ValidateAsync` orchestrator required by `IModule<TModule>`. `ValidateAsync` creates one editor-hidden `ModuleValidationContext` containing the runtime and service provider and passes it through all generated preparation phases.

Collection traversal is guarded according to the concrete collection shape. Null reference collections and absent nullable value-type collections are skipped. A default `ImmutableArray<T>` is not enumerated and remains distinct from an initialized empty array; property-level constraints such as `[Required]` still execute outside the enumeration guard, allowing invalid default arrays to produce validation errors instead of throwing while validating elements. Selected validation attributes can set `TargetsElements = true` to validate each immediate collection element through the same guarded traversal. Element-targeted errors retain the parent property name and include the zero-based element index in their message.

After generated validation completes, workers may optionally adjust the result through `ModuleValidationCallbackAsync`. If debugging is enabled, the complete result is offered to the workflow debugger before validity is enforced, allowing breakpoints and stepping to inspect failed prepared modules together with their errors. On a continue-to-execution path, the pipeline then calls `EnsureValid()`, which throws a `ValidationException` if any errors remain. Only after successful validation does the worker's `ExecuteAsync` method execute.

#### Execution and Result

Every module execution returns an `IModuleExecutionResult` containing the executed module instance, a `ModuleExitStatus` (`Success`, `Failed`, `Skipped`, or `Canceled`), and an artifact scope holding the module's published outputs.

Workers return results via builder methods on `ModuleWorker<TModule>`: `Success()`, `Failed()`, `Skipped()`, and `Canceled()`, each optionally accepting an `IDecomposable` result object for structured artifact publishing. The `runtime.Exit(result)` call finalizes the result and publishes artifacts to the configured target environment.

### Runtime Hierarchy

Module execution is orchestrated by `IModuleRuntime`, which manages the environment hierarchy and child module dispatch. The runtime forms a tree rooted at `RootModuleRuntime`:

- **RootModuleRuntime** is the entry point. It holds the `GlobalRuntimeEnvironment`, the named environment registry, and the top-level execution surface.
- **ScopedRuntime** is created for each nested execution. It carries its own `IRuntimeEnvironment` but delegates environment registration and lookup upward through the runtime tree.

When a module calls `runtime.ExecuteAsync(...)`, the runtime prepares an `IRuntimeEnvironment` based on the requested scope, binds the module's namespace to the environment, creates a new `ScopedRuntime` wrapping that environment, and invokes the module worker's `ExecuteAsync` within the scoped runtime.

### Environment Binding

Each module executes within a bound environment — one whose `Namespace` property is set to the module's effective namespace. The effective namespace uses the most specific available identifier in this order: `Name`, `Group`, then `ModuleId`. This namespace determines how override resolution, artifact paths, self-references, and default artifact namespaces operate for that module.

## Runtime Environment

The runtime environment subsystem manages the hierarchical variable stores that modules use to communicate. It encompasses environment scoping, variable resolution with indirection and interpolation, a property override mechanism for late-binding module configuration, and structured artifact publishing for module outputs. Together, these components form the data flow backbone of the execution model.

### Environment Scoping

Environments form a hierarchical variable store. Each module executes in an environment determined by the `EnvironmentScope` declared in its `ModuleContext`.

#### Scope Types

| Scope | Behavior | Typical Use |
|-------|----------|-------------|
| `InheritParent` | New environment with fallback to the immediate parent | Default for most modules |
| `Isolated` | New environment with no variable inheritance | Sandboxed execution |
| `Global` | Execute directly in the global singleton environment | Cross-job shared state |
| `InheritGlobal` | New environment inheriting only from global (skipping parent chain) | Read global config, ignore local overrides |
| `Parent` | Bind directly to the parent's environment (shared, no copy) | In-place variable mutation, flatten execution layers |
| `Current` | Use the current environment as-is | Equivalent to `Parent`, describes intent from the caller's perspective |
| `Reference` | Reference a previously created named environment by name | Cross-step state sharing |

#### Environment Types

The environment hierarchy is implemented by three types:

- **RuntimeEnvironment** — Base environment backed by a dictionary. Supports variable resolution, interpolation, override resolution, and artifact publishing.
- **InheritedRuntimeEnvironment** — Extends `RuntimeEnvironment` with a parent link. Variable lookups first check the local dictionary, then fall through to the parent chain.
- **GlobalRuntimeEnvironment** — A singleton root environment. The starting point for all global-scoped lookups.

When a scope is `InheritParent`, the runtime creates an `InheritedRuntimeEnvironment` whose parent is the current module's environment. Variables set locally shadow the parent, but unresolved lookups propagate upward.

#### Named Environments

Environments declared with an explicit `name` (and not marked `transient`) are registered in the root runtime's environment registry. Any subsequent module can access them via `Reference` scope by name. This enables cross-step state sharing — for example, a guard module's `finally` block can reference the same named environment as the `body` block to read variables set during normal execution.

Transient environments (those without an explicit name, or marked `transient: true`) receive a generated unique name and are not registered.

### Variable Resolution

Variables are the primary communication mechanism between modules. The resolution subsystem supports direct lookup, indirection, interpolation, type-safe access, and cycle detection.

#### Resolution Semantics

When `TryResolveVariable<T>(name)` is called, the runtime captures the environment where the lookup started as the **entry point**. Resolution proceeds as follows:

1. **Current-scope self-reference** — The special name `@` resolves to the environment namespace in the environment tree where resolution is currently occurring.
2. **Entry-point self-reference** — The special name `@@` resolves to the namespace of the environment that initiated the current resolution or interpolation chain, effectively resetting the resolution scope back to the entry point for any lookups within that chain. This allows for late-bound references to the entry-point scope even when resolution has propagated into parent environments.
3. **Direct lookup** — The variable name is looked up in the local dictionary.
4. **Indirection** — If the stored value is a string matching the pattern `${...}`, the referenced expression is resolved recursively. `${name}` resolves relative to the current resolution scope, `${@name}` resolves relative to the entry-point scope, `${@}` resolves the current scope namespace, and `${@@}` resolves the entry-point namespace.
5. **Interpolation** — If the stored value is a string containing `${...}` placeholders mixed with literal text, all placeholders are replaced with their resolved values using the same scope rules. Unresolvable placeholders are left as-is.
6. **Parent fallback** — In an `InheritedRuntimeEnvironment`, if the variable is not found locally, the lookup is delegated to the parent chain.
7. **Type casting** — The resolved value is matched against the requested type `T`. A type mismatch is treated as a resolution failure.

#### Cycle Detection

The resolution subsystem tracks the chain of variable names during recursive resolution via a linked `ResolutionContext`. If a variable name appears twice in the resolution chain, an `InvalidOperationException` is thrown to prevent infinite loops.

#### Variable Name Syntax

Cyborg distinguishes between identifiers, namespaces, variable expressions, interpolation, and indirection. Names and expressions are case-sensitive and use ASCII characters only.

**Identifiers** are used for environment variable names and paths, module `name` and `group` values, template argument names, override-resolution tags, and the identifier portion of override keys.

An identifier consists of one or more dot-separated segments:

- The first segment must begin with an ASCII letter, underscore, or hyphen.
- The remaining characters of the first segment may be ASCII letters, decimal digits, underscores, or hyphens.
- Every segment following a dot must contain at least one ASCII letter, decimal digit, underscore, or hyphen.

Dots are the only hierarchical delimiters. They cannot appear at the beginning or end of an identifier, and two dots cannot occur consecutively.

Hyphens and underscores are ordinary identifier characters. They may appear at the beginning or end of a segment and may occur consecutively. As a result, identifiers such as `-`, `--`, `_`, `-host_`, `_host-`, `host--name`, `host.-`, and `host.--` are valid.

A decimal digit cannot begin the first segment, but it may begin a segment following a dot. For example, `host.0` is valid while `0host` is not.

Other valid identifiers include `host`, `_internal`, `host.port`, `backup-job.result`, and `a-.b`.  
Invalid identifiers include `.host`, `host.`, `host..port`, `host port`, and `${host}`.

**Namespaces** currently use the same syntax as identifiers. They are maintained as a distinct grammar contract because namespace and general identifier syntax may diverge in the future.

**Variable expressions** are enclosed in `${` and `}` and use one of the following forms:

- `${name}` resolves an identifier from the current resolution scope.
- `${@name}` resolves an identifier from the entry-point scope.
- `${@}` resolves to the namespace of the current resolution scope.
- `${@@}` resolves to the namespace of the entry-point scope.

The `name` portion must be a valid identifier. Forms such as `${}`, `${1name}`, `${name.}`, `${@1name}`, and `${@@name}` are not valid variable expressions.

**Interpolation** occurs when one or more valid variable expressions appear within a larger string. Each recognized expression is resolved independently while surrounding text remains unchanged. For example, `backup-${host.name}-${date}` contains two interpolation expressions.

Malformed `${...}` text is not recognized as an interpolation expression and is ignored. A hash immediately after `${` escapes one interpolation pass: `${#name}` becomes the literal `${name}`, `${##name}` becomes `${#name}`, and the expression revealed by removing the hash is not rescanned during the same pass. `$${name}` is not an escape because it still contains the valid expression `${name}`.

**Indirection** is the special case where the complete string consists of exactly one variable expression, with no surrounding text. Indirection allows the referenced value to retain its original type rather than being converted into part of an interpolated string. So the following substitution chain is valid for `port` of type `int`:

```text
host_port = 8080
@host.port = "${host_port}"
```

**Override keys** begin with an at-sign (`@`) followed by a valid identifier, for example `@backup.target`. The leading at-sign is override syntax and is not part of the identifier itself.

Structured values published through [decomposition](#decomposable-objects) are addressed using the same identifier and dotted-path syntax. Override lookup uses these rules when constructing the candidates described in [Module Property Overrides](#module-property-overrides).

**Full grammar** for identifiers, namespaces, variable expressions, and override keys is as follows (ANTLR4 syntax):

```antlr
grammar VariableGrammar;

identifier
    : IDENTIFIER EOF
    ;

namespaceName
    : IDENTIFIER EOF
    ;

indirection
    : interpolation EOF
    ;

interpolation
    : '${' expression '}'
    ;

expression
    : '@@'
    | '@' IDENTIFIER?
    | IDENTIFIER
    ;

IDENTIFIER
    : IDENTIFIER_PREFIX
      IDENTIFIER_CHAR*
      ('.' IDENTIFIER_CHAR+)*
    ;

fragment IDENTIFIER_PREFIX
    : [A-Za-z_-]
    ;

fragment IDENTIFIER_CHAR
    : [A-Za-z_0-9-]
    ;
```

#### Decomposable Objects

Types annotated with `[GeneratedDecomposition]` implement `IDecomposable`, which exposes their properties as `DynamicKeyValuePair` entries. When such an object is published to the environment, its properties become individually addressable variables. For example, publishing a remote host object makes `host.hostname`, `host.port`, and other properties available as individual variables.

The `DecompositionStrategy` controls how deeply nested objects are flattened:

| Strategy | Behavior |
|----------|----------|
| `LeavesOnly` | Only leaf (non-decomposable) values are published as variables |
| `Shallow` | Top-level properties are published; nested decomposables become single entries |
| `FullHierarchy` | The root and all nested decomposables are published at every level, allowing access to complex-typed intermediate nodes |

### Module Property Overrides

The override subsystem allows runtime environment variables to replace module properties after deserialization. This is the mechanism used by the source-generated `ResolveOverridesAsync()` pipeline.

#### Override Resolution

Generated override preparation resolves module properties through `ModuleValidationContext`, which mediates internal operations on `IRuntimeEnvironment`:

1. The generator supplies the module and property expressions used to derive the snake_case property path.
2. Override keys are constructed using every identifier that can address the module instance: first `@{name}.{property_name}`, then `@{group}.{property_name}` when a group is set, then `@{module_id}.{property_name}`, and finally `@{tag}.{property_name}` for each override resolution tag attached to the environment.
3. The environment is checked for each override key in that order. The first matching override wins, so more specific identifiers take priority (`name` > `group` > `module_id` > tags).
4. String properties select the raw stored override without interpolation. Non-string properties use typed resolution, including exact-reference indirection, and collections use a collection-specific resolver before generated code materializes the declared collection shape.
5. The later generated `ApplyInterpolationAsync` phase recursively interpolates every eligible string, including strings for which no override was applied and strings inside nested records and collections. `[IgnoreInterpolation]` skips this phase, so a raw string selected from an override remains available for worker-controlled interpolation.

`[IgnoreOverride]` disables resolution of the annotated node without disabling the later interpolation phase. With the default `recurse: false`, eligible descendants may still resolve overrides; `recurse: true` suppresses the complete subtree. `[IgnoreInterpolation]` is a separate string-only control for values that must remain raw until worker execution.

#### Override Use Case

Overrides solve the problem of injecting non-string typed values into module properties through exact-reference indirection. A composite interpolation result is a string, but an exact expression such as `${host.port}` can resolve directly to the stored `int`. By setting `@my_module.liveness_probe_port` to `"${host.port}"`, generated typed override resolution retrieves the referenced value as the property type rather than converting an interpolated string afterward.

The override subsystem supports any addressable property on the module, including `ModuleReference` properties, allowing modules to be treated as data and enabling dynamic module composition patterns. Overrides always operate in a deterministic copy-on-write manner — the original deserialized module instance is never mutated. Instead, a new instance is returned with freshly resolved properties for each execution, ensuring that module definitions remain immutable while always observing the latest environment state at execution time.

Override resolution is applied recursively within module properties, so a complex-typed property instance may have overrides applied to its own properties as well.

The generated pipeline applies defaults before override resolution and again afterward. Overrides must therefore produce values that satisfy the module's constraints, but they can also inject a type-default value to trigger the second defaulting pass. Eligible strings are then processed by the recursive interpolation phase before constraints are evaluated.

#### Override Resolution Tags

Override resolution tags are additional identifiers attached to an environment that extend the override lookup chain beyond the built-in `Name`, `Group`, `ModuleId` sequence. They are set when preparing an environment via `PrepareEnvironment()` and stored on the `IRuntimeEnvironment`.

Tags are appended after `ModuleId` in the override resolution order, acting as a fallback. This mechanism enables parent modules to inject ambient overrides into child execution scopes without requiring knowledge of the child module's name or type. For example, a workflow orchestrator could tag an environment with `"production"`, causing any module executing in that environment to pick up overrides keyed under `@production.{property}`.

Because each tag becomes a variable-path identifier, it must satisfy the canonical variable-identifier grammar. `DynamicModule` interpolates and validates every configured tag before calling `PrepareEnvironment()`, so invalid user-provided tags fail module validation before child execution. `PrepareEnvironment()` retains the same identifier check as a runtime invariant for programmatic callers.

### Artifact Publishing

Artifacts are the structured output mechanism for modules. They allow a module's results to be exposed as environment variables accessible to parent or sibling modules, enabling dynamic, data-driven execution flows based on module outputs.

While modules can set arbitrary variables in the environment during execution, the artifact subsystem provides a structured way to declare namespaced outputs that are automatically published upon module completion. This makes state management and data flow between modules explicit and easier to reason about. Decomposable artifacts are preferable to ad-hoc ambient state mutation for communicating results between modules.

#### Artifact Lifecycle

1. **Collection** — During execution, a worker collects artifacts via the `IModuleArtifactsBuilder` (accessed through the `Artifacts` property on the worker). Artifacts can be scalar values, decomposable objects, or arbitrary data.
2. **Finalization** — When the worker returns via `runtime.Exit(result)`, the artifact builder is finalized. The module's exit status is added as a variable under the configured `ExitStatusName` (default: `$?`).
3. **Publishing** — The finalized artifacts are published to a target environment. The target is determined by `module.Artifacts.Environment`, which defaults to `Parent` scope — meaning artifacts flow to the parent module's environment.

#### Artifact Configuration

Each module carries a `ModuleArtifacts` record (inherited from `ModuleBase`) that controls publishing behavior:

| Field | Default | Purpose |
|-------|---------|---------|
| `Namespace` | Module's effective namespace | Variable path prefix for published artifacts |
| `ExitStatusName` | `$?` | Name of the variable holding the module's exit status |
| `Environment` | `Parent` scope | Target environment for artifact publication |
| `DecompositionStrategy` | `LeavesOnly` | How deeply decomposable results are flattened |
| `PublishNullValues` | `false` | Whether to publish null-valued properties |

These artifact properties can be overridden from the environment at runtime using the override subsystem, allowing dynamic control over artifact publishing behavior. Structural `ModuleBase.Name` and `ModuleBase.Group` are explicit exceptions and ignore both overrides and generated interpolation.

#### Artifact Exposure Patterns

Workers expose artifacts in two ways:

- **Scalar values** — `Artifacts.Expose("path", value)` sets a single variable at the given path within the artifact namespace.
- **Decomposable results** — Returning `Success(result)` or `Failed(result)` with an `IDecomposable` result automatically decomposes the object and publishes its properties according to the configured `DecompositionStrategy`.

After publishing, parent modules can read artifacts via standard variable resolution. For example, a subprocess module publishing a result with `ExitCode`, `Stdout`, and `Stderr` properties makes those values available as variables in the parent environment under the module's artifact namespace.

## Supporting Infrastructure

Beyond the module and environment systems, Cyborg provides several supporting subsystems that modules rely on for interacting with external processes, extracting structured data, and reporting operational metrics.

### Parsing Infrastructure

Cyborg includes a grammar-based parser combinator framework for extracting structured data from subprocess output. This subsystem is primarily used to parse borg command output into typed results for metrics extraction and programmatic consumption, but may also be used for validating unstructured input via the `[MatchesGrammar]` validation attribute.

#### Parser Combinators

The `Grammar` static factory provides a fluent builder API for composing parsers from smaller building blocks:

| Combinator | Behavior |
|------------|----------|
| `Sequence` | All child parsers must match in order. Supports up to eight type parameters for zero-allocation singleton composition via CRTP, or a builder API for more flexible nesting. |
| `Alternative` | Returns the result of the first child parser that matches. |
| `Optional` | Always succeeds. If the inner parser matches, produces a node wrapping the result; otherwise produces an empty node. |

Combinators can be nested arbitrarily to express complex grammars. The builder API supports naming sub-parsers for disambiguation when the same parser type appears in multiple positions within a grammar.

#### Terminal Parsers

Terminal parsers match text patterns via compiled regular expressions. The `RegexParserBase<TSelf>` abstract base class uses the curiously recurring template pattern to provide a static `Instance` singleton for each parser type. Each terminal parser implements the `IRegexOwner` static abstract interface, which exposes a `[GeneratedRegex]` property for AOT-safe, build-time compiled regular expressions.

All regex patterns must be anchored at the current parse offset (using the `\G` anchor) to ensure deterministic, position-based matching. On a successful match, the terminal parser produces a typed `ISyntaxNode` containing the parsed value.

#### Syntax Tree and Data Extraction

Parsers produce an `ISyntaxNode` tree with parent-linked nodes. Each node carries a name and supports upward traversal via `HasParent(name)`, enabling contextual discrimination when the same parser type appears in different positions within a grammar.

Data extraction from the syntax tree uses the visitor pattern. Consumers implement `INodeVisitor` with typed `Visit` methods for the syntax node types of interest. The `Accept` method on each node dispatches to the appropriate visitor method, allowing structured data to be collected in a single traversal.

#### Integration Points

The parsing infrastructure serves two roles:

1. **Output parsing** — Grammars are applied to subprocess stdout or stderr to extract structured results (e.g., borg archive statistics, prune counts). The parsed data is published as module artifacts or metrics.
2. **Input validation** — The `[MatchesGrammar]` validation attribute applies a grammar to a module property value at validation time, ensuring it conforms to an expected format (e.g., borg compression specifications).

### Process Execution

Subprocess execution is abstracted behind the `IChildProcessDispatcher` interface, which provides a single `ExecuteAsync` method accepting a `ProcessStartInfo` and returning a `ChildProcessResult` containing the exit code, captured standard output, and captured standard error.

The default implementation handles stream redirection, asynchronous output capture, and process tree termination on cancellation. All subprocess arguments are passed via `ProcessStartInfo.ArgumentList` (array-based) rather than string concatenation, preventing shell injection vulnerabilities. See the [Security Design Principles](#security-design-principles) section for details.

The `SubprocessModule` built-in module provides the JSON-configurable interface to this infrastructure, exposing options for impersonation, environment variable injection, exit code checking, and output capture configuration.

### Metrics Collection

Cyborg includes a Prometheus-compatible metrics collection subsystem. The `IMetricsCollector` interface supports creating labeled metrics in three standard types: counters, gauges, and untyped metrics. Each metric is registered with a name, description, and a builder callback that populates samples with label sets and values.

Modules contribute metrics during execution. The CLI entry point writes collected metrics to a file in Prometheus exposition format after module execution completes. Metric names follow the `cyborg_` prefix convention. Label collections are reusable across multiple metric samples.

## Cross-Cutting Concerns

The following architectural constraints and design principles apply across all subsystems. They are not localized to any single component but instead shape the overall system design and inform implementation decisions throughout the codebase.

### Security Design Principles

Cyborg executes backup workflows defined in JSON configuration files that may reference external templates, module definitions, and dynamically discovered paths. Because these workflows can invoke subprocesses with elevated privileges, the security model must address both the integrity of configuration inputs and the safety of subprocess execution. The following principles govern how the system defends against injection, privilege escalation, and unauthorized configuration.

#### Configuration File Trust

Cyborg workflows are composable — a top-level job can reference external module files, load templates by path, pull in configuration fragments, or enumerate files via glob patterns. Any of these external files, if writable by an unprivileged user, could be used to inject arbitrary subprocess commands or alter backup behavior. To mitigate this, Cyborg enforces a configuration file trust model that audits every external configuration file before it is deserialized.

Whenever the module configuration loader reads a file from disk — whether triggered by the CLI entry point, a template module, an external module, or an external configuration module — the trust subsystem evaluates the file against a set of configurable trust policies before deserialization proceeds. The evaluation produces a trust decision for the file, which aggregates individual policy decisions.

Each trust policy inspects a property of the file and returns one of three outcomes: accept (the file satisfies the policy), reject (the file violates the policy), or abstain (the policy is not applicable in the current environment, for example a Unix permissions policy running on Windows). A file is considered trusted only if no policy rejects it. All policies are evaluated regardless of earlier rejections, so the trust decision captures the complete set of violations for diagnostic purposes.

The built-in policies target Unix environments, where file ownership and permission bits are the primary access control mechanism:

- **Owner policy** — Verifies that the file is owned by a user or group from an explicit allow list (e.g., only `root`). Abstains on non-Linux platforms.
- **Permissions policy** — Verifies that specific permission bits are present (e.g., owner-readable) and that specific bits are absent (e.g., group-writable, other-writable, setuid, setgid). Abstains on Windows.

The trust subsystem operates in one of three enforcement modes, configured globally:

| Mode | Behavior |
|------|----------|
| `Enforce` | A rejected file causes a security exception, halting execution before the file is deserialized. |
| `LogOnly` | Rejected files are logged but execution continues. Useful during initial deployment to audit existing file permissions without breaking workflows. |
| `Disabled` | Trust evaluation is skipped entirely. |

Trust policies are themselves configured through the dynamic value system, allowing deployments to define custom policy sets with environment-specific allowed owners and permission requirements.

#### Subprocess Safety

All subprocess invocations use array-based argument passing (`ProcessStartInfo.ArgumentList`), never string concatenation or shell interpretation. This prevents command injection via `$()`, backticks, or argument injection via `;`, `&&`, `||`, and similar shell metacharacters.

#### Input Validation

Each module validates inputs through the source-generated validation pipeline after deserialization and immediately before execution. Validation attributes enforce constraints on property values — string patterns via `[MatchesRegex]` and `[MatchesGrammar]`, file system existence via `[FileExists]` and `[DirectoryExists]`, numeric ranges via `[Range<T>]`, and required fields via `[Required]`. This ensures that invalid or potentially dangerous input is rejected before it reaches any execution logic.

#### Privilege Boundaries

Subprocess impersonation is controlled through explicit configuration on the `SubprocessModule`, with validation ensuring only permitted operations are expressed in the module configuration.

### AOT Compilation

Cyborg is designed for native AOT compilation, which imposes specific architectural constraints:

- **No reflection-based dependency injection** — The Jab compile-time DI container generates service resolution code at build time, avoiding `System.Reflection.Emit`.
- **No reflection-based JSON serialization** — All JSON deserialization uses source-generated `JsonSerializerContext` instances. Safe extension methods in the serialization infrastructure enforce this at the API level.
- **No dynamic type instantiation** — Source generators create factory methods for module loaders, validation pipelines, and model decomposition, eliminating the need for `Activator.CreateInstance` or similar patterns.
- **Trim-safe collections** — Module configuration properties use `ImmutableArray<T>` and `IReadOnlyCollection<T>` to avoid hidden allocations and ensure trim compatibility.

These constraints are enforced by the project configuration (`PublishAot=true`, `IsAotCompatible=true`, `PublishTrimmed=true`, `JsonSerializerIsReflectionEnabledByDefault=false`) and validated at build time.
