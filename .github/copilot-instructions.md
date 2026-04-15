# Cyborg

Cyborg is a .NET 10 domain-agnostic workflow orchestration framework with native AOT compilation. It uses a modular, JSON-configured architecture where workflows are composed from reusable module trees.

> **For architecture and design, refer to [/docs/architecture.md](/docs/architecture.md)** — the central hub linking to all detailed documentation.

## Project Structure

```
Source/
├── Cyborg.Cli/          # ConsoleAppFramework CLI entry point
├── Cyborg.Core/         # Core abstractions: modules, configuration, runtime, parsing
├── Cyborg.Core.Aot/     # Roslyn source generators (validation, loader factory, decomposition)
├── Cyborg.Core.Tests/   # Unit tests
├── Cyborg.Modules/      # Built-in modules (Sequence, Subprocess, Template, If, Foreach, etc.)
└── Cyborg.Modules.Borg/ # BorgBackup-specific modules
```

## Code Style

Full guide: [/code-style.md](/code-style.md)

- **No `var`** — always explicit types: `FileStream stream = new(...);`
- **Naming** — `_camelCase` private fields, `s_camelCase` statics, `SCREAMING_CASE` constants
- **Async** — suffix with `Async`, always await Tasks
- **Visibility** — always explicit; seal/static all internal types
- **Collections** — `ImmutableArray<T>` for module configuration properties

## Key Patterns

- **Three-part module pattern** — Module record (`IModule`) + Worker (`ModuleWorker<T>`) + Loader (`[GeneratedModuleLoaderFactory]`)
- **Jab DI** — compile-time DI via `[ServiceProviderModule]` / `[Import<T>]`
- **Environment scoping** — hierarchical variable scopes (`Isolated`, `InheritParent`, `Global`, etc.)
- **Source generators** — validation, loader factory, decomposition, contract bootstrap (see [/docs/architecture/source-generators.md](/docs/architecture/source-generators.md))

## Creating a New Module

1. Define module record in `Cyborg.Modules/{Name}/` implementing `IModule`
2. Create worker inheriting `ModuleWorker<TModule>`
3. Create loader with `[GeneratedModuleLoaderFactory]` attribute
4. Register in `ICyborgModuleServices.cs`
5. Add to `ModuleJsonSerializerContext.cs`

Details: [/docs/architecture/architecture-overview.md § Module System](/docs/architecture/architecture-overview.md)

## Build

```bash
cd Source
dotnet build Cyborg.slnx                     # Build all
dotnet test                                   # Run tests
./docker-build.sh -o /path/to/output          # AOT publish via Docker
```

## Key Documentation

| Topic | Path |
|-------|------|
| Architecture hub | [/docs/architecture.md](/docs/architecture.md) |
| Full architecture | [/docs/architecture/architecture-overview.md](/docs/architecture/architecture-overview.md) |
| Source generators | [/docs/architecture/source-generators.md](/docs/architecture/source-generators.md) |
| Module reference | [/docs/architecture/modules-reference.md](/docs/architecture/modules-reference.md) |
| Validation attributes | [/docs/architecture/validation-attributes-reference.md](/docs/architecture/validation-attributes-reference.md) |
| Dynamic values | [/docs/architecture/dynamic-values-reference.md](/docs/architecture/dynamic-values-reference.md) |
| Templates | [/docs/architecture/templates-reference.md](/docs/architecture/templates-reference.md) |
