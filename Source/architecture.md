# Cyborg Architecture

This document describes the system architecture and key design decisions for the Cyborg backup orchestration framework.

## Design Goals

1. **AOT Compilation** - Native AOT publishing for minimal startup time and memory footprint on Linux servers
2. **Extensibility** - Plugin-like module system allowing backup operations to be composed from JSON configuration
3. **Type Safety** - Compile-time verification of module registration and dependency injection
4. **Structured Output Parsing** - Grammar-based parsers for extracting metrics from borg subprocess output

## Project Structure

```
Cyborg.Core        ← Runtime abstractions (no source generators)
     ↑
Cyborg.Core.Aot    ← Roslyn source generators (netstandard2.0)
     ↑
Cyborg.Modules     ← Built-in module implementations
     ↑
Cyborg.Cli         ← Application entry point
```

**Why this layering?**
- `Cyborg.Core.Aot` targets netstandard2.0 (Roslyn analyzer requirement) and cannot reference the net10.0 runtime libraries
- Source generators are distributed as analyzers via `<ProjectReference OutputItemType="Analyzer">`
- `Cyborg.Modules` consumes generated code; `Cyborg.Core` provides runtime interfaces

## Module System Architecture

### Three-Part Module Pattern

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

### Module Composition via ModuleReference

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

### DI Module Registration Flow

```
Jab ServiceProvider
    │
    ├─ ICyborgCoreServices (module)
    │   └─ Registers: core services for the module composition system, prometheus metrics, parsing, etc.
    │
    └─ ICyborgModuleServices (module)
        └─ Registers: module implementations for specific operations (e.g., SequenceModuleLoader, SubprocessModuleLoader)
```

## Source Generator Architecture

### ModuleLoaderFactoryGenerator

Generates `CreateWorker()` implementations that resolve constructor dependencies:

```csharp
// Input (developer writes):
[GeneratedModuleLoaderFactory]
public sealed partial class FooModuleLoader(IServiceProvider sp)
    : ModuleLoader<FooModuleWorker, FooModule>(sp);

// Output (generated):
partial class FooModuleLoader
{
    protected override FooModuleWorker CreateWorker(FooModule module, IServiceProvider serviceProvider)
    {
        return new FooModuleWorker(
            module,
            serviceProvider.GetRequiredService<IOtherDependency>(),
            // ... additional constructor parameters resolved from DI
        );
    }
}
```

**Why generate this?**
- AOT requires avoiding `Activator.CreateInstance` and reflection-based DI
- Constructor parameters are analyzed at compile time
- Module type is passed directly; other parameters resolve from `IServiceProvider`
- Less boilerplate for developers creating new modules (no manual calls to `GetRequiredService`)

### JsonTypeInfoBindingsGenerator

Generates `GetTypeInfoOrDefault<T>()` for AOT-compatible JSON serialization:

```csharp
// Enables type-safe JSON deserialization without reflection:
JsonTypeInfo<FooModule>? info = context.GetTypeInfoOrDefault<FooModule>();
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

## Parsing Infrastructure

Grammar-based parser combinators for extracting structured data from borg output:

```
Grammar (factory)
    │
    ├─ Sequence(parsers...)   → SequentialSyntaxNode
    ├─ Alternative(parsers...)→ AlternativeSyntaxNode
    └─ Optional(parser)       → OptionalSyntaxNode

IParser
    │
    ├─ TryParse(input, offset) → ISyntaxNode + charsConsumed
    │
    └─ RegexParserBase<TSelf>  ← Terminal parsers with compiled regex
          │
          └─ IRegexOwner.ParserRegex (static abstract)
```

**Design decisions**:
- **Static abstract interface** (`IRegexOwner`) ensures regex is compiled once per parser type
- **Zero-allocation pre-check** via `Regex.IsMatch(ReadOnlySpan<char>)` before full match
- **Visitor pattern** support via `ISyntaxNode` for AST traversal

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

## JSON Configuration Schema

Module configurations use lower_snake_case properties (via `JsonKnownNamingPolicy.SnakeCaseLower`):

```json
{
  "cyborg.modules.sequence.v1": {
    "steps": [
      { "cyborg.modules.subprocess.v1": { "executable": "borg", "arguments": ["create", "::daily"] } }
    ]
  }
}
```

The entry point loads `config.json` containing a `TemplateModule` that routes to `daily.json`, `weekly.json`, etc. based on CLI argument.

## AOT Compatibility Constraints

1. **No reflection-based DI** - Use Jab `[ServiceProvider]`
2. **No `JsonSerializer.Deserialize<T>` without context** - Always use safe extension methods from `Cyborg.Core.Modules.Configuration.Serialization` that rely on source-generated bindings
3. **No dynamic type instantiation** - Source generators create factory methods
4. **Trim-safe collections** - Use `ImmutableArray<T>` (value type, no hidden allocations)

## Future Considerations

- **Secret management** - Allow modules to load sensitive configuration (e.g., borg repo passphrases) from public-key encrypted configurations (possible GitOps integration later)
- **Structured logging** - Integrate some AOT-compatible structured logging library for better observability of module execution