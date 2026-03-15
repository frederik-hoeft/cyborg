# Module System Architecture

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Three-Part Module Pattern](#three-part-module-pattern)
- [Module Execution Lifecycle](#module-execution-lifecycle)
- [Module Composition via ModuleReference](#module-composition-via-modulereference)
- [DI Module Registration Flow](#di-module-registration-flow)
- [Dependency Injection Architecture](#dependency-injection-architecture)
- [Metrics Architecture](#metrics-architecture)

<!-- /code_chunk_output -->


## Three-Part Module Pattern

Each module consists of three types serving distinct responsibilities:

| Type | Responsibility | Lifetime |
|------|----------------|----------|
| `*Module` (record) | Immutable configuration data holder | Per-configuration |
| `*ModuleWorker` | Execution logic via `ExecuteAsync()` | Per-execution |
| `*ModuleLoader` | JSON → Module deserialization | Singleton |

**Why separate Module from Worker?**
- **Immutability**: Module records are pure data, safe to cache or serialize
- **Testability**: Workers can be unit tested with constructed modules
- **DI integration**: Workers receive injected services; modules hold only configuration

## Module Execution Lifecycle

The `ModuleWorker<TModule>` base class orchestrates the complete lifecycle from raw configuration through validation to execution and artifact publishing:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ModuleWorker<TModule>                                │
│                                                                             │
│  IModuleWorker.ExecuteAsync(runtime, ct)                                    │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  1. VALIDATION PIPELINE  (IModule<TModule>)                         │    │
│  │     ┌────────────────────────────────────────────────────────────┐  │    │
│  │     │  ApplyDefaultsAsync()                                      │  │    │
│  │     │    └─ Fill null/default properties from [DefaultValue<T>]  │  │    │
│  │     │       annotations, recursively for nested records          │  │    │
│  │     ├────────────────────────────────────────────────────────────┤  │    │
│  │     │  ResolveOverridesAsync()                                   │  │    │
│  │     │    └─ Substitute ${VAR} references from runtime env        │  │    │
│  │     │    └─ Apply @module.property overrides                     │  │    │
│  │     ├────────────────────────────────────────────────────────────┤  │    │
│  │     │  ValidateAsync()                                           │  │    │
│  │     │    └─ Check [Required], [Range<T>], [ExactLength], etc.    │  │    │
│  │     │    └─ Returns ValidationResult<TModule>                    │  │    │
│  │     └────────────────────────────────────────────────────────────┘  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│         │                                                                   │
│         ▼                                                                   │
│  ┌──────────────────────────────────────┐                                   │
│  │  2. VALIDATION CALLBACK (optional)   │                                   │
│  │     ModuleValidationCallbackAsync()  │ ← Override for custom validation  │
│  └──────────────────────────────────────┘                                   │
│         │                                                                   │
│         ▼                                                                   │
│     EnsureValid() ─── throws if errors ──→ ValidationException              │
│         │                                                                   │
│         ▼                                                                   │
│     Module = validatedModule  ← Protected property now available            │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  3. EXECUTION  (abstract, implemented by concrete worker)           │    │
│  │     ExecuteAsync(runtime, ct)                                       │    │
│  │       └─ Access Module property (validated, with defaults/overrides)│    │
│  │       └─ Build Artifacts via Artifacts.Expose("name", value)        │    │
│  │       └─ Return runtime.Success(Module, Artifacts)                  │    │
│  │              or runtime.Failure(Module, Artifacts)                  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│         │                                                                   │
│         ▼                                                                   │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  4. ARTIFACT PUBLISHING  (handled by runtime on Success/Failure)     │   │
│  │     PublishArtifacts(module, artifacts)                              │   │
│  │       └─ Resolve target environment from module.Artifacts.Environment│   │
│  │       └─ Call artifacts.PublishToEnvironment(targetEnv)              │   │
│  │       └─ Each exposed artifact becomes a variable in target scope    │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Key Concepts:**

1. **Validation Before Execution** — The worker never accesses `Module` until validation succeeds. The generated `IModule<TModule>` implementation handles the three-stage pipeline (defaults → overrides → constraints).

2. **Immutable Transformation** — Each validation stage returns a new record instance via `with` expressions. The original deserialized module is never mutated.

3. **Artifact Publishing** — Workers expose outputs via `Artifacts.Expose("name", value)`. On success/failure, artifacts are published to the environment scope specified by `module.Artifacts.Environment` (defaults to current scope).

4. **Environment-Driven Overrides** — The override resolution stage enables late-binding of module properties from runtime variables, supporting patterns like `${host.port}` interpolation and `@module_name.property` typed overrides.

## Module Composition via ModuleReference

The `ModuleReference` type enables modules to contain other modules, creating arbitrarily nested execution trees:

```
JSON Config
    │
    ▼
ModuleReferenceJsonConverter.Read()
    │  ├─ Reads module ID property name
    │  ├─ Looks up IModuleLoader from registry
    │  └─ Delegates deserialization to loader
    ▼
ModuleReference { Module: IModuleWorker }
```

**Key insight**: The JSON structure uses the module ID as a property name wrapping its configuration:

```json
{ "cyborg.modules.subprocess.v1": { "executable": "borg", "arguments": [...] } }
```

This enables:
- **Polymorphic deserialization** without `$type` discriminators
- **Version-aware** module loading (IDs include `.v1`, `.v2`, etc.)
- **Registry-based** extensibility (new modules register loaders at startup)

## DI Module Registration Flow

```
Jab ServiceProvider
    │
    ├─ ICyborgCoreServices (module)
    │   └─ Registers: core services for the module composition system, prometheus metrics, parsing, etc.
    │
    └─ ICyborgModuleServices (module)
        └─ Registers: module implementations for specific operations (e.g., SequenceModuleLoader, SubprocessModuleLoader)
```

## Dependency Injection Architecture

Cyborg uses **Jab** for compile-time DI, avoiding runtime reflection:

```csharp
[ServiceProvider]
[Import<ICyborgCoreServices>]     // Core services module
[Import<ICyborgModuleServices>]   // Module implementations
internal sealed partial class DefaultServiceProvider;
```

**Why Jab over Microsoft.Extensions.DI?**
- Native AOT compatible (no `System.Reflection.Emit`)
- Compile-time validation of service registrations
- Single-file deployment without runtime codegen

**Service registration pattern**:
```csharp
[ServiceProviderModule]
[Singleton<IModuleLoader, SequenceModuleLoader>]
[Singleton<IModuleLoader, SubprocessModuleLoader>]
public interface ICyborgModuleServices;
```

Multiple `IModuleLoader` registrations are resolved as `IEnumerable<IModuleLoader>` by `DefaultModuleLoaderRegistry`.

## Metrics Architecture

Planned Prometheus metrics export for backup health monitoring:

```
PrometheusBuilder
    │
    ├─ AddSimpleMetric(name, value, labels)
    └─ AddMetric(name, type, samples)
            │
            ▼
    PrometheusMetric
        └─ WriteTo(StringBuilder)  → Prometheus exposition format
```

**Planned metrics**:
- `cyborg_backup_success{job="daily"}` - per-job success/failure
- `cyborg_backup_duration_seconds{job="daily"}` - execution time
- `cyborg_repo_size_bytes{repo="..."}` - borg repository statistics
- ... additional metrics parsed from borg output
