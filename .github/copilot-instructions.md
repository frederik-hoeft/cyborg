# Cyborg Backup Framework

Cyborg is a WIP .NET 10 application that will fully replace the legacy `borg/` shell scripts. It provides a modular, JSON-configured backup orchestration system with AOT compilation support.

**Status:** Proof-of-concept stage. Core module system is functional; testing infrastructure will be added as the project moves toward release.

## Architecture Overview

```
Source/
├── Cyborg.Cli/       # ConsoleAppFramework CLI entry point
├── Cyborg.Core/      # Core abstractions: modules, configuration, runtime, metrics, parsing
├── Cyborg.Core.Aot/  # Roslyn source generators for AOT-compatible JSON serialization and module loaders
└── Cyborg.Modules/   # Built-in modules: Sequence, Subprocess, Template
```

**Key patterns:**
- **Modules** define backup operations as immutable records implementing `IModule` with a static `ModuleId` (e.g., `"cyborg.modules.sequence.v1"`)
- **ModuleWorkers** execute module logic via `IModuleWorker.ExecuteAsync()`
- **ModuleLoaders** deserialize JSON config into modules; use `[GeneratedModuleLoaderFactory]` for AOT
- **Jab** provides compile-time DI via `[ServiceProviderModule]` interfaces (see [ICyborgCoreServices.cs](Source/Cyborg.Core/ICyborgCoreServices.cs), [ICyborgModuleServices.cs](Source/Cyborg.Modules/ICyborgModuleServices.cs))

## Module Composition

Modules compose via `ModuleReference`, enabling nested module trees in JSON configuration. The deserialization flow:

1. `ModuleReferenceJsonConverter` reads a module ID as a JSON property name (e.g., `"cyborg.modules.subprocess.v1"`)
2. Looks up the corresponding `IModuleLoader` from `DefaultModuleLoaderRegistry`
3. Delegates to `IModuleLoader.TryCreateModule()` which deserializes the module's JSON payload
4. Returns `ModuleReference` wrapping the created `IModuleWorker`

This enables modules like `SequenceModule` to contain child modules:

```csharp
// SequenceModule holds an array of ModuleReferences, each deserializing to any registered module type
public sealed record SequenceModule(ImmutableArray<ModuleReference> Steps) : IModule;
```

```json
{
  "cyborg.modules.sequence.v1": {
    "steps": [
      { "cyborg.modules.subprocess.v1": { "executable": "borg", "arguments": ["create", "..."] } },
      { "cyborg.modules.subprocess.v1": { "executable": "borg", "arguments": ["prune", "..."] } }
    ]
  }
}
```

See [ModuleReference.cs](Source/Cyborg.Core/Modules/Configuration/Model/ModuleReference.cs), [ModuleReferenceJsonConverter.cs](Source/Cyborg.Core/Modules/Configuration/Model/ModuleReferenceJsonConverter.cs), and [examples/](examples/) for composition patterns.

## Creating a New Module

1. **Define module record** in `Cyborg.Modules/{ModuleName}/`:
   ```csharp
   public sealed record FooModule(ImmutableArray<string> Items) : IModule
   {
       public static string ModuleId => "cyborg.modules.foo.v1";
   }
   ```

2. **Create worker** inheriting `ModuleWorker<TModule>`:
   ```csharp
   public sealed class FooModuleWorker(FooModule module) : ModuleWorker<FooModule>(module)
   {
       public override async Task<bool> ExecuteAsync(CancellationToken cancellationToken) { ... }
   }
   ```

3. **Create loader** with source generator:
   ```csharp
   [GeneratedModuleLoaderFactory]
   public sealed partial class FooModuleLoader(IServiceProvider sp) : ModuleLoader<FooModuleWorker, FooModule>(sp);
   ```

4. **Register** in [ICyborgModuleServices.cs](Source/Cyborg.Modules/ICyborgModuleServices.cs):
   ```csharp
   [Singleton<IModuleLoader, FooModuleLoader>]
   ```

5. **Add to JSON serializer context** in [ModuleJsonSerializerContext.cs](Source/Cyborg.Modules/ModuleJsonSerializerContext.cs):
   ```csharp
   [JsonSerializable(typeof(FooModule))]
   ```

## Code Style (from [code-style.md](code-style.md))

- **No `var`**: Always use explicit types (`FileStream stream = new(...)` not `var stream = ...`)
- **Naming**: `_camelCase` for private fields, `s_camelCase` for statics, `SCREAMING_CASE` for constants
- **Async**: Suffix async methods with `Async`, always await Tasks
- **Collections**: Use `ImmutableArray<T>` for module configuration properties
- **Visibility**: Always explicit (`private`, `internal`); seal/static internal types

## Build & Run

```bash
cd Source
dotnet build Cyborg.slnx          # Build all projects
dotnet run --project Cyborg.Cli -- run daily   # Run daily backup template
dotnet publish Cyborg.Cli -c Release           # AOT publish
```

Artifacts output to `Source/artifacts/` (configured in [Directory.Build.props](Source/Directory.Build.props)).

## JSON Configuration

Modules are configured via JSON with snake_case properties. Module IDs are versioned keys:

```json
{
  "cyborg.modules.sequence.v1": {
    "steps": [
      { "cyborg.modules.subprocess.v1": { "executable": "ls", "arguments": ["-la"] } }
    ]
  }
}
```

See [config.json](Source/Cyborg.Cli/config.json) for template registry, [daily.json](Source/Cyborg.Cli/daily.json) for sequence example.

## Key Files

| Purpose | Location |
|---------|----------|
| CLI commands | [Cyborg.Cli/Commands.cs](Source/Cyborg.Cli/Commands.cs) |
| DI composition root | [Cyborg.Cli/DefaultServiceProvider.cs](Source/Cyborg.Cli/DefaultServiceProvider.cs) |
| Module configuration loading | [Cyborg.Core/Modules/Configuration/](Source/Cyborg.Core/Modules/Configuration/) |
| Source generators | [Cyborg.Core.Aot/](Source/Cyborg.Core.Aot/) |
| Built-in modules | [Cyborg.Modules/Sequence/](Source/Cyborg.Modules/Sequence/), [Subprocess/](Source/Cyborg.Modules/Subprocess/), [Template/](Source/Cyborg.Modules/Template/) |
| Module composition | [ModuleReference.cs](Source/Cyborg.Core/Modules/Configuration/Model/ModuleReference.cs), [ModuleReferenceJsonConverter.cs](Source/Cyborg.Core/Modules/Configuration/Model/ModuleReferenceJsonConverter.cs) |
| Architecture decisions | [Source/architecture.md](Source/architecture.md) |

## Future Infrastructure

**Prometheus Metrics** (`Cyborg.Core/Metrics/`): Will export backup health metrics (success-per-job, borg repo stats). Uses `PrometheusBuilder` for metric construction.

**Parsing Infrastructure** (`Cyborg.Core/Parsing/`): Grammar-based parser combinators for parsing unstructured borg subprocess output. Key types:
- `Grammar` - static factory for building parsers (Sequence, Alternative, Optional)
- `IParser` - base interface with `TryParse()` returning `ISyntaxNode`
- Supports regex-based terminal parsers via `RegexParserBase`
