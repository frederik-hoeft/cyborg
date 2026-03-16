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

Providers are registered by type name (e.g., `"int"`, `"string"`, `"bool"`, `"cyborg.types.borg.remote.v1"`) in the `IDynamicValueProviderRegistry`. Typed collections use `collection<T>` syntax:

```json
{ "key": "backup_hosts", "collection<cyborg.types.borg.remote.v1>": [{ ... }, { ... }] }
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

Each module executes within a *bound* environment — one whose `Namespace` property is set to the module's effective namespace. The effective namespace is the module's `Name` property if set, otherwise its `ModuleId`. This namespace determines how artifact paths and override keys are constructed for that module.

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

When `TryResolveVariable<T>(name)` is called, the runtime captures the environment where the lookup started as the **entry point**. Recursive resolution then distinguishes between two reference modes:

- **Current-scope references** — `${name}` starts from the environment currently evaluating the value and follows normal parent fallback.
- **Entry-point references** — `${@name}` starts from the original entry-point environment that initiated the current resolution chain, even if the containing value was defined in a parent scope.
- **Self-reference** — `${@}` resolves to the current environment's namespace.

Resolution proceeds as follows:

1. **Self-reference** — The special name `@` resolves to the environment's current namespace, and may be referenced in both current-scope and entry-point modes (i.e., `${@}` or `${@@}`) to get the namespace of the current definition scope or the original entry point, respectively.
2. **Direct lookup** — The variable name is looked up in the local dictionary.
3. **Indirection** — If the stored value is a string matching the pattern `${variable_name}` or `${@variable_name}`, the referenced variable is resolved recursively using the corresponding lookup origin.
4. **Interpolation** — If the stored value is a string containing `${...}` placeholders mixed with literal text, all placeholders are replaced with their resolved values. Unresolvable placeholders are left as-is.
5. **Parent fallback** — In an `InheritedRuntimeEnvironment`, if the variable is not found locally, the lookup is delegated to the parent chain without changing the original entry point.
6. **Type casting** — The resolved value is matched against the requested type `T`. A type mismatch is treated as a resolution failure.

This allows parent-defined templates to late-bind variables from child scopes. For example, a parent scope can define:

```json
{
  "key": "repository_path",
  "string": "ssh://${@host.borg_user}@${@host.hostname}:${@host.port}${@host.borg_repo_root}/${container_name}"
}
```

and a child scope can later resolve `${repository_path}`. The `${@host...}` placeholders are evaluated from the child environment that initiated the lookup, while `${container_name}` still resolves from the current definition scope using normal rules.

### Cycle Detection

The resolution system tracks the chain of variable references during recursive resolution via a linked `ResolutionContext`. Cycle detection includes both the variable name and its lookup origin, so `${name}` and `${@name}` are treated as distinct references. If the same reference appears twice in the active resolution chain, an `InvalidOperationException` is thrown to prevent infinite loops.

### Variable Name Syntax

Variable names follow the pattern `[A-Za-z_][A-Za-z_0-9\-\.]*`. Dots serve as hierarchical separators (e.g., `host.port`), enabling structured access into decomposed objects. Inside interpolation and indirection expressions, the runtime also supports an entry-point prefix: `${@host.port}` starts lookup from the original resolution entry point instead of the current definition scope.

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
2. An override key is constructed: `@{namespace}.{property_name}`, where the namespace is the module's `Name` (if set) or its `ModuleId`.
3. The environment is checked for a variable matching the override key. If found, its value replaces the module property.
4. If no override is found and the property value is a string, it is interpolated (replacing `${...}` and `${@...}` placeholders). Entry-point placeholders continue to resolve from the environment that initiated override resolution, not from the scope where the override string was defined.

### Override Use Case

Overrides are an extremely powerful feature for injecting dynamic values into module properties at binding time, just-in-time before individual module execution. They solve the problem of injecting non-string typed values into module properties when the source is a string variable. String interpolation (`${host.port}`) always produces a string, but a module property like `liveness_probe_port` expects an `int`. By setting `@my_module.liveness_probe_port` to `"${host.port}"` in the environment, the override system interpolates the string, then type-converts the result to the target type. The override system supports any addressable property on the module, including `ModuleReference` properties, allowing modules to be treated as data and enabling dynamic module composition patterns.

Note that overrides always operate in a deterministic copy-on-write manner — the original deserialized module instance is never mutated. Instead, a new instance is returned with freshly resolved properties for each execution. This ensures that module definitions remain immutable and free of side effects, while modules always observe the latest environment state at execution time.

The override resolution is ordered — the module's `Name` is checked before its `ModuleId`, allowing instance-specific overrides to take priority over type-level overrides.

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
