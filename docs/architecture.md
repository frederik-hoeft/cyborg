# Cyborg Architecture

This document describes the system architecture and key design decisions for the Cyborg backup orchestration framework.

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

## Overview

Cyborg is a .NET 10 application providing modular, JSON-configured backup orchestration with native AOT compilation support. It replaces legacy bash-based backup scripts with a type-safe, extensible module system.

## Documentation

| Document | Description |
|----------|-------------|
| [Core Concepts](architecture/core-concepts.md) | Design goals, project structure, AOT constraints |
| [Module System](architecture/module-system.md) | Three-part module pattern, lifecycle, composition, DI |
| [Source Generators](architecture/source-generators.md) | Roslyn generators for AOT-compatible code |
| [Parsing Infrastructure](architecture/parsing.md) | Grammar-based parsers for subprocess output |
| [Runtime Infrastructure](architecture/runtime.md) | Environment scoping, variable resolution, configuration |
| [Migration Design](architecture/migration.md) | Legacy workflow analysis, migration goals |
| [Module Reference](architecture/modules-reference.md) | Complete module documentation and status |
| [Configuration Examples](architecture/configuration-examples.md) | Scoping patterns, job configurations |
| [Implementation](architecture/implementation.md) | Priorities, security, metrics, extension points |

## Quick Reference

### Design Goals

1. **AOT Compilation** - Native AOT publishing for minimal startup time and memory footprint
2. **Extensibility** - Plugin-like module system with JSON configuration
3. **Type Safety** - Compile-time verification of module registration and DI
4. **Structured Output Parsing** - Grammar-based parsers for extracting metrics

### Project Layers

```
Cyborg.Core        ← Runtime abstractions (no source generators)
     ↑
Cyborg.Core.Aot    ← Roslyn source generators (netstandard2.0)
     ↑
Cyborg.Modules     ← Built-in module implementations
     ↑
Cyborg.Cli         ← Application entry point
```

### Module Status

Legend: ✅ Implemented | 🚧 Partial/WIP | ❌ Planned

| Category | Modules |
|----------|---------|
| Control Flow | ✅ Sequence, ✅ ForEach, ✅ Conditional, 🚧 Guard |
| Configuration | ✅ ConfigMap, ✅ ConfigCollection, ✅ Template |
| Named Modules | ✅ Definition, ✅ Reference |
| Subprocess | ✅ Subprocess |
| Network | ✅ Wake-on-LAN, 🚧 SSH Shutdown, ❌ Ping |
| Borg | ❌ Repository, ❌ Create, ❌ Prune, ❌ Compact |
| Services | ❌ Docker Up/Down, ❌ Systemd Start/Stop |
| Security | ❌ Secrets Load |
| System | ❌ Run-As |
| Logging | ❌ Log |

See [Module Reference](architecture/modules-reference.md) for complete documentation.
