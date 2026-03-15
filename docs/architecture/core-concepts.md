# Core Concepts

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Design Goals](#design-goals)
- [Project Structure](#project-structure)
- [JSON Configuration Schema](#json-configuration-schema)
- [AOT Compatibility Constraints](#aot-compatibility-constraints)
- [Future Considerations](#future-considerations)

<!-- /code_chunk_output -->


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
