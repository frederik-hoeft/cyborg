# Cyborg

Cyborg is a .NET 10 workflow orchestration engine that composes complex, multi-step workflows from declarative JSON configuration. It compiles to a single native AOT binary with no runtime dependencies, designed for unattended operation on Linux servers. While its core engine is fully domain-agnostic, the current distribution includes a module library for BorgBackup orchestration as its primary use case.

## Overview

Cyborg provides a Turing-complete module system where every operation — from executing subprocesses to orchestrating multi-host backup workflows — is expressed as a composable module in JSON. Modules can be nested, parameterized, and reused through templates, enabling complex workflows without writing code. All core APIs (module composition, environment scoping, variable resolution, validation, artifact publishing) are domain-agnostic and designed to be extended with custom module libraries for any orchestration task.

Key capabilities:

- **Declarative workflows** — Jobs are defined as JSON configuration files that compose built-in modules for sequencing, conditionals, loops, subprocess execution, and domain-specific operations.
- **Template system** — Reusable workflow templates with parameterized overrides, enabling shared patterns across services (e.g., a common Docker or systemd backup template applied to different containers or services).
- **Borg module library** — Built-in support for borg archive creation, pruning, and compaction across multiple remote repositories, with Wake-on-LAN for cold backup targets.
- **Prometheus metrics** — Automatic export of operational statistics in Prometheus exposition format.
- **Native AOT binary** — Compiles to a self-contained executable with no .NET runtime dependency, minimal startup time, and low memory footprint.
- **Configuration trust** — File ownership and permission auditing on configuration files to prevent privilege escalation through tampered workflows.

## Use Cases

Cyborg's core engine is domain-agnostic — any workflow that can be expressed as a composition of subprocess calls, conditionals, loops, and environment variable passing can be orchestrated through JSON configuration. The included `Cyborg.Modules.Borg` library provides a ready-made solution for BorgBackup orchestration.

### BorgBackup Orchestration

The sample configuration in `samples/` demonstrates a deployment where backup targets may be powered down when idle and must be woken on demand. A typical workflow involves a Linux server that:

1. Wakes remote backup hosts via Wake-on-LAN.
2. Stops dependent services (Docker containers, systemd units) to ensure data consistency.
3. Creates borg archives across one or more remote repositories.
4. Prunes and compacts repositories according to retention policies.
5. Restarts services and optionally shuts down remote hosts.
6. Exports Prometheus metrics for monitoring.

All of these steps are expressed as a single JSON workflow file referencing shared templates, with host-specific configuration and secrets loaded from separate files.

## Getting Started

### Prerequisites

- Docker (for containerized builds), or .NET 10 SDK (for building from source)
- For borg workflows: BorgBackup installed on the backup host(s) and SSH access to remote repositories

### Building

**Using the build script** (recommended):

```bash
# Build inside Docker and output to Source/artifacts/
Source/docker-build.sh

# Build and output to a custom directory
Source/docker-build.sh -o /usr/local/bin
```

**Manually with Docker**:

```bash
docker build --target artifact --output type=local,dest=./dist Source/
```

**Manually with the .NET SDK**:

```bash
cd Source
dotnet publish Cyborg.Cli/Cyborg.Cli.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true
```

Build artifacts are output to `Source/artifacts/`.

### Configuration

Cyborg is configured through jconf files, which are JSON with support for comments.

Cyborg expects its configuration in `/etc/cyborg/` by default. The `samples/` directory provides a complete reference configuration:

| File | Purpose |
|------|---------|
| `cyborg.jconf` | Main workflow entry point — defines the top-level module to execute |
| `cyborg.options.jconf` | Runtime options: logging, metrics, trust policies |
| `cyborg.hosts.jsecrets` | Host definitions and secrets (borg passphrases, SSH settings, WoL MACs) |
| `jobs/` | Per-frequency job definitions (daily, weekly) |
| `templates/` | Reusable workflow templates (Docker backup, systemd backup) |

Copy the sample files to `/etc/cyborg/`, adjust host definitions and secrets for your environment, and ensure configuration files are owned by root with restrictive permissions (see [Security](#security) below).

### Running

```bash
# Execute the daily backup target
cyborg run -e target=daily

# Execute with a custom configuration path
cyborg run --main /path/to/cyborg.jconf -e target=daily

# Override the console log level
cyborg run -e target=daily --log-level information
```

The `target` environment variable selects which job to run (e.g., `daily`, `weekly`). Additional environment variables can be injected via `-e` with optional type annotations (e.g., `-e port:int=2222`).

## Configuration Model

Workflows are defined as JSON files using versioned module IDs and snake_case property names:

```json
{
  "module": {
    "cyborg.modules.sequence.v1": {
      "steps": [
        {
          "module": {
            "cyborg.modules.subprocess.v1": {
              "command": { "executable": "/usr/bin/borg", "arguments": ["create", "::daily"] }
            }
          }
        }
      ]
    }
  }
}
```

Each module invocation is wrapped in a `ModuleContext` envelope that can declare environment scoping, configuration modules for variable injection, and pre-execution requirements. Modules compose arbitrarily — a sequence can contain conditionals, each branch can run loops over parameterized templates, and templates can reference external configuration files.

For details on the configuration model and all available modules, see the [Module Reference](docs/architecture/modules-reference.md).

## Security

Cyborg workflows can execute subprocesses with elevated privileges. To prevent privilege escalation through tampered configuration files, the trust subsystem audits file ownership and permissions before any configuration file is deserialized. The default policy requires configuration files to be owned by root and not writable by group or other users.

Trust enforcement is configurable in `cyborg.options.jconf` and supports three modes: `enforce` (block untrusted files), `log_only` (warn but continue), and `disabled`. See the [Security Design Principles](docs/architecture/architecture-overview.md#security-design-principles) section of the architecture documentation for details.

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture Overview](docs/architecture/architecture-overview.md) | System architecture: module system, runtime, environment scoping, parsing, security |
| [Module Reference](docs/architecture/modules-reference.md) | Complete documentation of all built-in modules |
| [Dynamic Values Reference](docs/architecture/dynamic-values-reference.md) | Dynamic value providers and typed configuration |
| [Templates Reference](docs/architecture/templates-reference.md) | Template module usage and patterns |
| [Source Generators](docs/architecture/source-generators.md) | Roslyn source generators for AOT-compatible code generation |
| [Validation Attributes Reference](docs/architecture/validation-attributes-reference.md) | Validation, defaulting, and override control attributes |

## Project Structure

```
Source/
  Cyborg.Cli/           Application entry point and CLI routing
  Cyborg.Core/           Core abstractions: modules, runtime, parsing, services
  Cyborg.Core.Aot/       Roslyn source generators for AOT compatibility
  Cyborg.Modules/        Built-in modules (sequence, subprocess, template, etc.)
  Cyborg.Modules.Borg/   Borg-specific modules (create, prune, compact)
samples/                 Reference configuration files and templates
docs/                    Architecture and reference documentation
```

## Extending Cyborg

Cyborg's architecture separates the domain-agnostic engine (`Cyborg.Core`, `Cyborg.Modules`) from domain-specific module libraries (`Cyborg.Modules.Borg`). Cyborg is licensed under the MIT License. To adapt Cyborg for a different orchestration domain, fork the repository and replace or extend the domain-specific layer:

1. **Create a new module library** — Add a project alongside or in place of `Cyborg.Modules.Borg`. Each module follows the three-part pattern (module record, worker, loader) described in the [Architecture Overview](docs/architecture/architecture-overview.md#three-part-module-pattern). Annotate the module record with `[GeneratedModuleValidation]` and the loader with `[GeneratedModuleLoaderFactory]` to have the source generators produce the validation pipeline and deserialization factory.

2. **Register modules via a service interface** — Expose a Jab `[ServiceProviderModule]` interface that registers your module loaders (as `IModuleLoader` singletons), dynamic value providers, and any supporting services. This follows the same pattern as `ICyborgBorgServices`.

3. **Import into the CLI composition root** — Add an `[Import<IYourModuleServices>]` attribute to `DefaultServiceProvider` in `Cyborg.Cli` and register any additional `JsonSerializerContext` instances required by your module types.

4. **Provide JSON configuration** — Define workflow files using your module IDs. All engine-level modules (sequence, if/else, foreach, template, subprocess, guard, etc.) remain available and compose freely with custom modules.

The core engine, built-in flow-control modules, environment scoping, variable resolution, override system, and validation infrastructure require no modification. Custom modules participate in all of these subsystems automatically through the source-generated interfaces.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
