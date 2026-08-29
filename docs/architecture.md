# Cyborg Architecture

Cyborg is a .NET 10 application providing modular, JSON-configured workflow orchestration with native AOT compilation support.

## Documentation

| Document | Description |
|----------|-------------|
| [System Architecture](architecture/architecture-overview.md) | Comprehensive architecture overview: module lifecycle, runtime, scoping, host configuration, descriptions, parsing, and core subsystems |
| [Transactional Execution](architecture/transactions.md) | Per-invocation transactions and DI scopes, snapshot isolation, reconciliation, parallel execution, and transaction-aware services |
| [Module Reference](architecture/modules-reference.md) | Complete documentation of all built-in modules |
| [Dynamic Values Reference](architecture/dynamic-values-reference.md) | Dynamic value providers, typed configuration, tagged strings, and secrets |
| [Interpolation and Overrides](architecture/interpolation.md) | Expression syntax, resolution phases, override selection, and deferred interpolation |
| [Templates Reference](architecture/templates-reference.md) | Template module usage and patterns |
| [Source Generators](architecture/source-generators.md) | Roslyn source generators for AOT-compatible code generation |
| [Validation Attributes Reference](architecture/validation-attributes-reference.md) | Complete reference for validation, defaulting, override, and interpolation control attributes |
| [Module Testing](architecture/module-testing.md) | Production-backed module test adapter and dedicated source-generator fixture assembly |
| [Workflow Debugging](architecture/debugging.md) | Breakpoints, debugger frontend boundaries, module descriptions, inspection, and console REPL architecture |

For operational metric output, see [Metrics](metrics.md).
