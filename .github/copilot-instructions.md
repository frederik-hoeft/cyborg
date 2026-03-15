# Cyborg Backup Framework

Cyborg is a WIP .NET 10 application that will fully replace the legacy `borg/` shell scripts. It provides a modular, JSON-configured backup orchestration system with AOT compilation support.

**Status:** Proof-of-concept stage. Core module system is functional; testing infrastructure will be added as the project moves toward release.

> **For design and architectural work, refer to [/docs/architecture.md](/docs/architecture.md)** — it contains comprehensive documentation on module patterns, environment scoping, parsing infrastructure, planned modules, and migration strategy.

## Project Structure

```
Source/
├── Cyborg.Cli/       # ConsoleAppFramework CLI entry point
├── Cyborg.Core/      # Core abstractions: modules, configuration, runtime, metrics, parsing
├── Cyborg.Core.Aot/  # Roslyn source generators for AOT-compatible JSON serialization
└── Cyborg.Modules/   # Built-in modules: Sequence, Subprocess, Template, Borg, Named
```

## Key Patterns

- **Modules** — Immutable records implementing `IModule` with static `ModuleId`
- **ModuleWorkers** — Execute logic via `IModuleWorker.ExecuteAsync()`
- **ModuleLoaders** — Deserialize JSON → modules using `[GeneratedModuleLoaderFactory]`
- **Jab DI** — Compile-time DI via `[ServiceProviderModule]` interfaces
- **Environment Scoping** — Hierarchical variable scopes (Isolated, Global, InheritParent, etc.)
- **Grammar Parsing** — Parser combinators for extracting structured data from subprocess output

See [architecture.md](/Source/architecture.md) for detailed design documentation.

## Creating a New Module

1. Define module record in `Cyborg.Modules/{ModuleName}/` implementing `IModule`
2. Create worker inheriting `ModuleWorker<TModule>`
3. Create loader with `[GeneratedModuleLoaderFactory]` attribute
4. Register in [ICyborgModuleServices.cs](/Source/Cyborg.Modules/ICyborgModuleServices.cs)
5. Add to [ModuleJsonSerializerContext.cs](/Source/Cyborg.Modules/ModuleJsonSerializerContext.cs)

Detailed steps and examples: [architecture.md § Module System Architecture](/Source/architecture.md)

## Code Style (from [code-style.md](/code-style.md))

- **No `var`**: Always use explicit types (`FileStream stream = new(...)`)
- **Naming**: `_camelCase` for private fields, `s_camelCase` for statics, `SCREAMING_CASE` for constants
- **Async**: Suffix async methods with `Async`, always await Tasks
- **Collections**: Use `ImmutableArray<T>` for module configuration properties
- **Visibility**: Always explicit (`private`, `internal`); seal/static internal types

Before contributing, please review the full style guide in [code-style.md](/code-style.md).

## Build & Run

```bash
cd Source
dotnet build Cyborg.slnx                       # Build all projects
dotnet run --project Cyborg.Cli -- run daily   # Run daily backup template
dotnet publish Cyborg.Cli -c Release           # AOT publish
```

Artifacts output to `Source/artifacts/`.

## JSON Configuration

Module IDs are versioned keys with snake_case properties:

```json
{
  "cyborg.modules.sequence.v1": {
    "steps": [
      { "cyborg.modules.subprocess.v1": { "executable": "ls", "arguments": ["-la"] } }
    ]
  }
}
```

## Key Files

| Purpose | Location |
|---------|----------|
| Architecture & design | [/Source/architecture.md](/Source/architecture.md) |
| CLI commands | [/Source/Cyborg.Cli/Commands.cs](/Source/Cyborg.Cli/Commands.cs) |
| DI composition root | [/Source/Cyborg.Cli/DefaultServiceProvider.cs](/Source/Cyborg.Cli/DefaultServiceProvider.cs) |
| Module configuration | [/Source/Cyborg.Core/Modules/Configuration/](/Source/Cyborg.Core/Modules/Configuration/) |
| Runtime & scoping | [/Source/Cyborg.Core/Modules/Runtime/](/Source/Cyborg.Core/Modules/Runtime/) |
| Parsing infrastructure | [/Source/Cyborg.Core/Parsing/](/Source/Cyborg.Core/Parsing/) |
| Source generators | [/Source/Cyborg.Core.Aot/](/Source/Cyborg.Core.Aot/) |
| Built-in modules | [/Source/Cyborg.Modules/](/Source/Cyborg.Modules/) |
