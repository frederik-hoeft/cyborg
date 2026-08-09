# Cyborg Architecture

Cyborg is a .NET 10 application providing modular, JSON-configured backup orchestration with native AOT compilation support.

## Documentation

| Document | Description |
|----------|-------------|
| [System Architecture](architecture/architecture-overview.md) | Comprehensive architecture overview: module lifecycle, runtime, scoping, host configuration, descriptions, parsing, and core subsystems |
| [Module Reference](architecture/modules-reference.md) | Complete documentation of all built-in modules |
| [Dynamic Values Reference](architecture/dynamic-values-reference.md) | Dynamic value providers and typed configuration |
| [Templates Reference](architecture/templates-reference.md) | Template module usage and patterns |
| [Source Generators](architecture/source-generators.md) | Roslyn source generators for AOT-compatible code generation |
| [Validation Attributes Reference](architecture/validation-attributes-reference.md) | Complete reference for validation, defaulting, override, and interpolation control attributes |
| [Module Testing](architecture/module-testing.md) | Production-backed module test adapter and dedicated source-generator fixture assembly |
| [Workflow Debugging](architecture/debugging.md) | Breakpoints, debugger frontend boundaries, module descriptions, inspection, and console REPL architecture |
