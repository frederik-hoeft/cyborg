# Cyborg System Architecture

This document provides a comprehensive overview of the Cyborg system architecture. It covers the module system, JSON deserialization, execution model, environment scoping, variable resolution, property overrides, artifact publishing, parsing infrastructure, process execution, metrics, and security. After reading this document, you should have a clear understanding of how the system is structured, how modules are loaded and executed, and how the major subsystems interact.

For detailed reference material, see:

- [Module Reference](modules-reference.md) — Complete documentation of all built-in modules
- [Dynamic Values Reference](dynamic-values-reference.md) — Dynamic value providers and typed configuration
- [Templates Reference](templates-reference.md) — Template module usage and patterns
- [Source Generators](source-generators.md) — Roslyn source generators for AOT-compatible code generation
- [Validation Attributes Reference](validation-attributes-reference.md) — Validation, defaulting, and override control attributes

**Table of Contents**

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Overview](#overview)
- [Project Structure](#project-structure)
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

The solution is organized into four primary layers, each with a specific role in the dependency hierarchy:

| Layer | Target | Purpose |
|-------|--------|---------|
| `Cyborg.Core` | net10.0 | Core abstractions: module interfaces, runtime, environment scoping, configuration, parsing, and cross-cutting services. Contains no module-specific logic. |
| `Cyborg.Core.Aot` | netstandard2.0 | Roslyn incremental source generators distributed as analyzers. Targets netstandard2.0 as required by the Roslyn analyzer hosting model. |
| `Cyborg.Modules` | net10.0 | Built-in, domain-agnostic module implementations supplemented by generated code from `Cyborg.Core.Aot`, e.g., for model validation and instance activation. |
| `Cyborg.Modules.Borg` | net10.0 | Borg-specific modules (create, prune, compact) with JSON output parsing and borg-specific configuration types. |
| `Cyborg.Cli` | net10.0 | Application entry point using ConsoleAppFramework for CLI routing, with Jab for compile-time dependency injection composition. |

`Cyborg.Core` defines the runtime interfaces and abstractions. `Cyborg.Core.Aot` generates code that implements those interfaces for specific module types. `Cyborg.Modules` and `Cyborg.Modules.Borg` provide the built-in module library. `Cyborg.Cli` composes everything into the final executable. Each module library exposes a Jab `[ServiceProviderModule]` interface (e.g., `ICyborgModuleServices`, `ICyborgBorgServices`) that the CLI project imports into its composition root.

## Module System

The module system is the central architectural pattern in Cyborg. Every unit of work — from executing a subprocess to orchestrating a multi-step backup workflow — is represented as a module.

### Three-Part Module Pattern

Each module consists of three types serving distinct responsibilities:

| Type | Responsibility | Lifetime |
|------|----------------|----------|
| Module (record) | Immutable configuration data holder. Pure data, safe to cache or transform. Inherits from `ModuleBase` and implements `IModule`. | Per-configuration |
| Worker | Execution logic. Inherits from `ModuleWorker<TModule>`, receives injected services, and implements module behavior through the abstract `ExecuteAsync` method. | Per-configuration, stateless |
| Loader | JSON deserialization. Inherits from `ModuleLoader<TWorker, TModule>` with a source-generated factory method that constructs the worker from the deserialized module record and dependency-injected services. | Singleton |

Before execution, the immutable module record is copied and transformed through the validation pipeline, applying defaults, and per-execution overrides. The worker operates on the fully validated module instance, ensuring that execution logic never encounters invalid configuration and that deserialized module definitions remain immutable and free of execution-time side effects.

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

Custom types implement `IDynamicValueProvider` and register a versioned type name. When annotated with `[GeneratedDecomposition]`, they gain `IDecomposable` support for hierarchical property access via the variable resolution subsystem. See the [Dynamic Values Reference](dynamic-values-reference.md) for a complete listing of available value providers.

## Module Execution

This section describes how modules are executed at runtime: the validation pipeline that prepares module records before execution, the runtime hierarchy that manages nested module dispatch, and the environment binding model that determines each module's execution context.

### Execution Lifecycle

The `ModuleWorker<TModule>` base class orchestrates the complete lifecycle from raw configuration through validation to execution and artifact publishing. This lifecycle ensures that no worker ever operates on unvalidated configuration.

#### Validation Pipeline

Before a worker's `ExecuteAsync` method is invoked, the base class runs the module through a source-generated three-stage validation pipeline implemented by `IModule<TModule>`:

1. **Apply Defaults** — Fills null or zero-valued properties from `[DefaultValue<T>]`, `[DefaultInstance]`, `[DefaultInstanceFactory]`, and `[DefaultTimeSpan]` annotations. Operates recursively on nested records marked with `[Validatable]`.
2. **Resolve Overrides** — Substitutes module properties from runtime environment variables using the override resolution subsystem (described in [Module Property Overrides](#module-property-overrides)). String-typed property values containing `${...}` expressions are interpolated against the current environment.
3. **Validate** — Checks constraints declared via validation attributes such as `[Required]`, `[Range<T>]`, `[MinLength]`, `[MaxLength]`, `[ExactLength]`, `[FileExists]`, `[DirectoryExists]`, `[MatchesRegex]`, `[MatchesGrammar]`, and `[DefinedEnumValue]`. Produces a `ValidationResult<TModule>` containing any errors.

Each stage returns a new record instance via `with` expressions — the original deserialized module is never mutated. After the generated pipeline completes, workers may optionally implement `ModuleValidationCallbackAsync` for custom validation logic. The pipeline then calls `EnsureValid()`, which throws a `ValidationException` if any errors were recorded. Only after successful validation does the worker's `ExecuteAsync` method execute.

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

The runtime environment subsystem manages the hierarchical variable stores that modules use to communicate. It encompasses a hierarchical variable store, variable resolution with indirection and interpolation, a property override mechanism for late-binding module configuration, and structured artifact publishing for module outputs. Together, these components form the data flow backbone of the execution model.

### Environment Scoping

Environments form a hierarchical variable store. Each module executes in an environment determined by the `EnvironmentScope` declared in its `ModuleContext`.

### Supporting Infrastructure

Beyond the module and environment systems, Cyborg provides several supporting subsystems that modules rely on for interacting with external processes, extracting structured data, and reporting operational metrics.

### Parsing Infrastructure

Cyborg includes a grammar-based parser combinator framework for extracting structured data from subprocess output into typed results and metrics.

### Process Execution

Subprocess execution is abstracted behind the `IChildProcessDispatcher` interface, which provides a single `ExecuteAsync` method accepting a `ProcessStartInfo` and returning a `ChildProcessResult` containing the exit code, captured standard output, and captured standard error.

### Metrics Collection

Cyborg includes a Prometheus-compatible metrics collection subsystem. The `IMetricsCollector` interface supports creating labeled metrics in three standard types: counters, gauges, and untyped metrics. Each metric is registered with a name, description, and a builder callback that populates samples with label sets and values.

Modules contribute metrics during execution. The CLI entry point writes collected metrics atomically to a file in Prometheus exposition format after each run. Metric names are prefixed by the configured namespace, which defaults to `cyborg`.

The CLI always emits the global `cyborg_last_run_success` gauge when it has successfully loaded the options configuration and initialized metrics. The value is `1` only when the top-level module returns `Success`; it is `0` for failed, skipped, canceled, or exceptional runs. Because this metric is written from the CLI run boundary, failures that occur before module execution or module-level metric collection—including main-module loading and deserialization failures—remain visible to monitoring.

## Cross-Cutting Concerns

The following architectural constraints and design principles apply across all subsystems. They are not localized to any single component but instead shape the system's implementation.

### Security Design Principles

Cyborg workflows may invoke subprocesses with elevated privileges, so configuration trust, validation, and argument-safe subprocess execution are treated as core security properties.
