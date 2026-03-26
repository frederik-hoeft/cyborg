# Runtime Infrastructure

This document describes the runtime architecture that underpins module execution in Cyborg. It covers JSON deserialization, the execution model, environment scoping, variable resolution, property overrides, and artifact publishing. After reading this document, you should have a clear understanding of how modules are loaded, executed, and how they communicate through the environment.

For module lifecycle details (validation pipeline, three-part pattern), see [Module System](module-system.md). For scoping patterns and full job configurations, see [Configuration Examples](configuration-examples.md).

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Module Loading and Deserialization](#module-loading-and-deserialization)
  - [The `ModuleContext` Envelope](#the-modulecontext-envelope)
  - [Registry-Based Deserialization](#registry-based-deserialization)
  - [Dynamic Value System](#dynamic-value-system)
- [Module Execution Model](#module-execution-model)
  - [Runtime Hierarchy](#runtime-hierarchy)
  - [Environment Binding](#environment-binding)
  - [Execution Result](#execution-result)
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

<!-- /code_chunk_output -->


## Module Loading and Deserialization

Cyborg uses a dynamic, registry-based JSON deserialization model where the module ID serves as both a discriminator and a version identifier. This avoids `$type` discriminators and supports polymorphic module composition while remaining AOT-compatible.

### The `ModuleContext` Envelope

Every module invocation in JSON is represented as a `ModuleContext` — a three-part envelope that separates concerns:

| Field | Purpose |
|-------|---------|
| `module` | The module to execute, identified by its versioned module ID |
| `environment` | Optional scoping configuration for the execution environment |
| `configuration` | Optional configuration module that populates the environment before the main module runs |

When a `ModuleContext` is deserialized, the `configuration` module (if present) executes first, populating the environment with variables. The main `module` then executes within that prepared environment.

### Registry-Based Deserialization

Module JSON uses the module ID as a property key wrapping the module's configuration:

```json
{ "cyborg.modules.subprocess.v1": { "executable": "borg", "arguments": ["create", "::daily"] } }
```

When the JSON deserializer encounters a `ModuleReference`, the `ModuleReferenceJsonConverter` reads the property name as a module ID, looks up the corresponding `IModuleLoader` from the `IModuleLoaderRegistry`, and delegates deserialization to that loader. The loader produces an `IModuleWorker` ready for execution. This registry-based dispatch enables version-aware loading and extensibility — new modules only need to register a loader at startup.

### Dynamic Value System

Configuration modules populate the environment with typed values using the `IDynamicValueProvider` system. Each entry in a configuration map is a key–value pair where the value type is a property name in the JSON:

```json
{ "key": "max_retries", "int": 3 }
{ "key": "container_name", "string": "overleaf" }
```

Providers are registered by type name (e.g., `"int"`, `"string"`, `"bool"`, `"cyborg.types.borg.remote.v1.4"`) in the `IDynamicValueProviderRegistry`. Typed collections use `collection<T>` syntax:

```json
{ "key": "backup_hosts", "collection<cyborg.types.borg.remote.v1.4>": [{ ... }, { ... }] }
```

Custom types implement `IDynamicValueProvider` and register a versioned type name. When annotated with `[GeneratedDecomposition]`, they gain `IDecomposable` support for hierarchical property access (e.g., `${host.hostname}`, `${host.port}`).

## Module Execution Model

### Runtime Hierarchy

Module execution is orchestrated by `IModuleRuntime`, which manages the environment hierarchy and child module dispatch. The runtime forms a tree:

```
RootModuleRuntime (owns global environment + environment registry)
│
├── ScopedRuntime (child environment, parent = root)
│   ├── ScopedRuntime (grandchild environment, parent = parent)
│   └── ...
└── ...
```

- `RootModuleRuntime` is the entry point. It holds the `GlobalRuntimeEnvironment`, the named environment registry, and the top-level execution surface.
- `ScopedRuntime` is created for each nested execution. It carries its own `IRuntimeEnvironment` but delegates environment registration and lookup upward through the runtime tree.

When a module calls `runtime.ExecuteAsync(...)`, the runtime:
1. Prepares an `IRuntimeEnvironment` based on the requested scope
2. Binds the module's namespace to the environment
3. Creates a new `ScopedRuntime` wrapping that environment
4. Invokes the module worker's `ExecuteAsync` within the scoped runtime

### Environment Binding

Each module executes within a *bound* environment — one whose `Namespace` property is set to the module's effective namespace. The effective namespace uses the most specific available identifier in this order: `Name`, `Group`, then `ModuleId`. This namespace determines how artifact paths, self references, and default artifact namespaces are constructed for that module.

### Execution Result

Every module execution returns an `IModuleExecutionResult` containing:

| Field | Purpose |
|-------|---------|
| `Module` | The module instance that was executed |
| `Status` | One of `Success`, `Failed`, `Skipped`, `Canceled` |
| `Artifacts` | A variable resolver scope containing the module's published outputs |

Workers return results via builder methods on `ModuleWorker<TModule>`: `Success()`, `Failed()`, `Skipped()`, `Canceled()`, each optionally accepting an `IDecomposable` result object. The `runtime.Exit(result)` call finalizes the result and publishes artifacts to the configured target environment.

## Environment Scoping

Environments form a hierarchical variable store. Each module executes in an environment determined by the `EnvironmentScope` declared in its `ModuleContext`.

### Scope Types

| Scope | Behavior | Typical Use |
|-------|----------|-------------|
| `InheritParent` | New environment with fallback to the immediate parent | Default for most modules |
| `Isolated` | New environment with no variable inheritance | Sandboxed execution |
| `Global` | Execute directly in the global singleton environment | Cross-job shared state |
| `InheritGlobal` | New environment inheriting only from global (skipping parent chain) | Read global config, ignore local overrides |
| `Parent` | Bind directly to the parent's environment (shared, no copy) | In-place variable mutation, flatten execution layers |
| `Current` | Use the current environment as-is | Equivalent to `Parent` |
| `Reference` | Reference a previously created named environment by name | Cross-step state sharing |

### Environment Types

The environment hierarchy is implemented by three types:

- **`RuntimeEnvironment`** — Base environment backed by a `Dictionary<string, object?>`. Supports variable resolution, interpolation, override resolution, and artifact publishing.
- **`InheritedRuntimeEnvironment`** — Extends `RuntimeEnvironment` with a parent link. Variable lookups first check the local dictionary, then fall through to the parent chain.
- **`GlobalRuntimeEnvironment`** — A singleton root environment. The starting point for all global-scoped lookups.

When a scope is `InheritParent`, the runtime creates an `InheritedRuntimeEnvironment` whose parent is the current module's environment. Variables set locally shadow the parent, but unresolved lookups propagate upward.

### Named Environments

Environments declared with an explicit `name` (and not marked `transient`) are registered in the root runtime's environment registry. Any subsequent module can access them via `Reference` scope:

```json
{
  "environment": { "scope": "inherit_parent", "name": "backup_session" },
  "module": { ... }
}
```

A later step can reference the same environment:

```json
{
  "environment": { "scope": "reference", "name": "backup_session" },
  "module": { ... }
}
```

This enables cross-step state sharing — for example, a `Guard` module's `finally` block can reference the same named environment as the `body` block to read variables set during normal execution.

Transient environments (those without an explicit name, or marked `transient: true`) receive a generated unique name and are not registered.

## Variable Resolution

Variables are the primary communication mechanism between modules. The resolution system supports direct lookup, indirection, interpolation, type-safe access, and cycle detection.

### Resolution Semantics

When `TryResolveVariable<T>(name)` is called, the runtime captures the environment where the lookup started as the **entry point**. Resolution proceeds as follows:

1. **Current-scope self-reference** — The special name `@` resolves to the current environment namespace.
2. **Entry-point self-reference** — The special name `@@` resolves to the namespace of the environment that initiated the current resolution or interpolation chain.
3. **Direct lookup** — The variable name is looked up in the local dictionary.
4. **Indirection** — If the stored value is a string matching the pattern `${...}`, the referenced expression is resolved recursively. `${name}` resolves relative to the current resolution scope, `${@name}` resolves relative to the entry-point scope, `${@}` resolves the current scope namespace, and `${@@}` resolves the entry-point namespace.
5. **Interpolation** — If the stored value is a string containing `${...}` placeholders mixed with literal text, all placeholders are replaced with their resolved values using the same scope rules. Unresolvable placeholders are left as-is.
6. **Parent fallback** — In an `InheritedRuntimeEnvironment`, if the variable is not found locally, the lookup delegated to the parent chain.
7. **Type casting** — The resolved value is matched against the requested type `T`. A type mismatch is treated as a resolution failure.

### Cycle Detection

The resolution system tracks the chain of variable names during recursive resolution via a linked `ResolutionContext`. If a variable name appears twice in the resolution chain, an `InvalidOperationException` is thrown to prevent infinite loops.

### Variable Name Syntax

Variable names follow the pattern `[A-Za-z_][A-Za-z_0-9\-\.]*`. Dots serve as hierarchical separators (e.g., `host.port`), enabling structured access into decomposed objects.

Within `${...}` expressions, the following special forms are also supported:

- `${@}` — current scope namespace (`Name`, then `Group`, then `ModuleId` for bound module environments)
- `${@@}` — entry-point scope namespace (`Name`, then `Group`, then `ModuleId` for the environment that initiated the resolution chain)
- `${name}` — normal lookup from the current resolution scope
- `${@name}` — late-bound lookup from the entry-point scope

### Decomposable Objects

Types annotated with `[GeneratedDecomposition]` implement `IDecomposable`, which exposes their properties as `DynamicKeyValuePair` entries. When such an object is published to the environment, its properties become individually addressable variables:

```
host → BorgRemote { Hostname = "backup1", Port = 22, ... }
host.hostname → "backup1"
host.port → 22
```

The `DecompositionStrategy` controls how deeply nested objects are flattened:

| Strategy | Behavior |
|----------|----------|
| `LeavesOnly` | Only leaf (non-decomposable) values are published as variables |
| `Shallow` | Top-level properties are published; nested decomposables become single entries |
| `FullHierarchy` | The root and all nested decomposables are published at every level, allowing access to complex-typed intermediate nodes |

## Module Property Overrides

The override system allows runtime environment variables to replace module properties after deserialization. This is the mechanism used by the source-generated `ResolveOverridesAsync()` pipeline.

### Override Resolution

When a module property is resolved via `IRuntimeEnvironment.Resolve<TModule, T>()`:

1. The property name is extracted from the call site using `CallerArgumentExpression` and converted to snake_case.
2. Override keys are constructed for every identifier that can address the module instance: first `@{name}.{property_name}`, then `@{group}.{property_name}` when a group is set, then `@{module_id}.{property_name}`, and finally `@{tag}.{property_name}` for each override resolution tag attached to the environment.
3. The environment is checked for each override key in that order. The first matching override wins, so more specific identifiers take priority (`name` > `group` > `module_id` > tags).
4. After override resolution, if the resulting property value is a string, it is interpolated (replacing `${...}` placeholders).

### Override Use Case

Overrides are an extremely powerful feature for injecting dynamic values into module properties at binding time, just-in-time before individual module execution. They solve the problem of injecting non-string typed values into module properties when the source is a string variable. String interpolation (`${host.port}`) always produces a string, but a module property like `liveness_probe_port` expects an `int`. By setting `@my_module.liveness_probe_port` to `"${host.port}"` in the environment, the override system interpolates the string, then type-converts the result to the target type. The override system supports any addressable property on the module, including `ModuleReference` properties, allowing modules to be treated as data and enabling dynamic module composition patterns.

Note that overrides always operate in a deterministic copy-on-write manner — the original deserialized module instance is never mutated. Instead, a new instance is returned with freshly resolved properties for each execution. This ensures that module definitions remain immutable and free of side effects, while modules always observe the latest environment state at execution time.

The override resolution is ordered — for each property, `Name` is checked first, then `Group`, then `ModuleId`, and finally any override resolution tags, allowing more specific overrides to take precedence over more general ones. Override resolution is applied recursively within module properties, so a complex-typed property instance may have overrides applied to its own properties as well.

### Override Resolution Tags

Override resolution tags are additional identifiers attached to an environment that extend the override lookup chain beyond the built-in `Name` → `Group` → `ModuleId` sequence. They are set when preparing an environment via `PrepareEnvironment(moduleEnvironment, overrideResolutionTags)` and stored on the `IRuntimeEnvironment`.

Tags are appended after `ModuleId` in the override resolution order. This means they act as a fallback — they only match if no override was found via the module's `Name`, `Group`, or `ModuleId`. Tag values must be valid identifiers.

This mechanism enables parent modules to inject ambient overrides into child execution scopes without requiring knowledge of the child module's name or type. For example, a workflow orchestrator could tag an environment with `"production"`, causing any module executing in that environment to pick up overrides keyed under `@production.{property}`.

Also note that overrides are resolved before default values are applied and before constraints are validated in the source-generated validation pipeline. This means that overrides must produce valid values that satisfy the module's constraints and validation attributes, and they can also be used to erase values to trigger default value substitution (e.g., setting `@my_module.port` to `0` to trigger a `[DefaultValue<int>]` of `22`).

## Artifact Publishing

Artifacts are the structured output mechanism for modules. They allow a module's results to be exposed as environment variables accessible to parent or sibling modules, with the goal of enabling dynamic, data-driven execution flows based on module outputs.

While modules can set arbitrary variables in the environment during execution, the artifact system provides a structured way to declare namespaced outputs that are automatically published upon module completion, making state management and data flow between modules more explicit and easier to reason about. As such, decomposable artifacts are preferable to ad-hoc ambient state mutation for communicating results between modules.

### Artifact Lifecycle

1. **Collection** — During execution, a worker collects artifacts via the `IModuleArtifactsBuilder` (accessed through `Artifacts` on the worker). Artifacts can be scalar values, decomposable objects, or arbitrary data.
2. **Finalization** — When the worker returns via `runtime.Exit(result)`, the artifact builder is finalized. The module's exit status is added as a variable under the configured `ExitStatusName` (default: `$?`).
3. **Publishing** — The finalized artifacts are published to a target environment. The target is determined by `module.Artifacts.Environment`, which defaults to `Parent` scope — meaning artifacts flow to the parent module's environment.

### Artifact Configuration

Each module carries a `ModuleArtifacts` record (inherited from `ModuleBase`) that controls publishing behavior:

| Field | Default | Purpose |
|-------|---------|---------|
| `Namespace` | Module's effective namespace | Variable path prefix for published artifacts |
| `ExitStatusName` | `$?` | Name of the variable holding the module's exit status |
| `Environment` | `Parent` scope | Target environment for artifact publication |
| `DecompositionStrategy` | `LeavesOnly` | How deeply decomposable results are flattened |
| `PublishNullValues` | `false` | Whether to publish null-valued properties |

Like all module properties, these can be overridden from the environment at runtime using the override system (e.g., `@my_module.artifacts.environment`), allowing dynamic control over artifact publishing behavior.

### Artifact Exposure Patterns

Workers expose artifacts in two ways:

- **Scalar values** — `Artifacts.Expose("path", value)` sets a single variable at the given path within the artifact namespace.
- **Decomposable results** — Returning `Success(result)` or `Failed(result)` with an `IDecomposable` result automatically decomposes the object and publishes its properties according to the configured `DecompositionStrategy`.

After publishing, parent modules can read artifacts via standard variable resolution. For example, a subprocess module publishing `SubprocessModuleResult(ExitCode, Stdout, Stderr)` makes `exit_code`, `stdout`, and `stderr` available as variables in the parent environment.
