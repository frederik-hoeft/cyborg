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

---

# Backup Orchestration Migration Design

This section documents the design for migrating the legacy bash-based backup scripts (`/borg/`) to the declarative .NET Cyborg framework.

## Migration Goals

1. **Functional parity** - Replicate all existing backup workflows (daily/weekly/monthly jobs)
2. **Declarative configuration** - JSON-driven backup definitions with no embedded shell scripts
3. **Injection-safe** - No raw command strings; all arguments are typed records validated at deserialization
4. **Composable modules** - Granular modules that compose into complex workflows
5. **Observable** - Prometheus metrics for backup health, duration, and borg statistics
6. **AOT-compatible** - All modules must work with native AOT compilation

## Legacy Workflow Analysis

### High-Level Execution Flow (from `borg-run.sh`)

```
1. Validate required tools exist
2. Load configuration (borg.conf, borg.secrets, borg.hosts.json)
3. For each backup host: Wake-on-LAN if offline, wait for SSH
4. Execute job scripts for current frequency (daily/weekly/monthly)
5. Cleanup: Shutdown any hosts that were woken up
```

### Job Script Pattern (common to all jobs)

Each job script follows this pattern:

```
1. Load job-specific secrets (BORG_PASSPHRASE)
2. Stop dependent service (Docker compose or systemd)
3. For each backup host:
   a. borg create (with compression, excludes)
   b. borg prune (with retention policy)
   c. borg compact
4. Restart dependent service
5. On any error: restart service, abort
```

### Services Managed

| Type | Bash Function | Purpose |
|------|---------------|---------|
| Docker Compose | `docker_down`, `docker_up` | Stop/start container groups via `su -c` as `DOCKER_USER` |
| Systemd | `service_down`, `service_up` | Stop/start systemd services |

### Borg Configuration Elements

| Element | Source | Description |
|---------|--------|-------------|
| Host list | `borg.hosts.json` | Hostname, port, WoL MAC, SSH command, repo root |
| Repository | Constructed | `ssh://borg@{host}:{port}{repo_root}/{repo_name}` |
| Passphrase | `*.secrets` | Per-repository encryption key |
| Compression | Per-job | `zlib`, `lz4`, etc. |
| Excludes | Per-job | Glob patterns for excluded paths |
| Retention | Per-job | `--keep-daily`, `--keep-weekly`, `--keep-monthly` |

## Module Design

### Module Hierarchy

```
Cyborg.Modules/
├── Borg/                    # Borg backup operations
│   ├── Create/              # cyborg.modules.borg.create.v1
│   ├── Prune/               # cyborg.modules.borg.prune.v1
│   ├── Compact/             # cyborg.modules.borg.compact.v1
│   └── Repository/          # cyborg.modules.borg.repository.v1
├── Services/                # Service lifecycle management
│   ├── Docker/              # cyborg.modules.docker.{up,down}.v1
│   └── Systemd/             # cyborg.modules.systemd.{start,stop}.v1
├── Network/                 # Network operations
│   ├── WakeOnLan/           # cyborg.modules.wol.wake.v1
│   ├── Shutdown/            # cyborg.modules.ssh.shutdown.v1
│   └── Ping/                # cyborg.modules.net.ping.v1
├── Control/                 # Control flow
│   ├── ForEach/             # cyborg.modules.foreach.v1
│   ├── Guard/               # cyborg.modules.guard.v1 (try-finally)
│   └── Conditional/         # cyborg.modules.if.v1
├── Security/                # Secrets management
│   └── Secrets/             # cyborg.modules.secrets.load.v1
└── System/                  # System operations
    └── RunAs/               # cyborg.modules.system.run_as.v1
```

---

## Core Control Flow Modules

### ForEach Module (`cyborg.modules.foreach.v1`)

Iterates over a collection in the environment, executing a child module for each item.

**Purpose:** Replace bash `for` loops over backup hosts, SMB targets, etc.

**Module Record:**
```csharp
public sealed record ForEachModule(
    string Collection,              // Environment variable name containing IEnumerable
    string ItemVariable,            // Variable name to bind current item
    string? IndexVariable,          // Optional: variable name for current index
    ModuleContext Body,             // Module to execute for each item
    bool ContinueOnError = false    // Continue iteration on body failure
) : IModule
{
    public static string ModuleId => "cyborg.modules.foreach.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.foreach.v1": {
    "collection": "backup_hosts",
    "item_variable": "current_host",
    "index_variable": "host_index",
    "continue_on_error": false,
    "body": {
      "module": { "cyborg.modules.borg.create.v1": { ... } }
    }
  }
}
```

**Worker Behavior:**
1. Resolve `collection` from runtime environment
2. For each item: set `item_variable` (and `index_variable`), execute `body`
3. If `continue_on_error` is false, stop on first failure
4. Return true only if all iterations succeeded

---

### Guard Module (`cyborg.modules.guard.v1`)

Executes a body module with guaranteed cleanup, similar to try-finally semantics.

**Purpose:** Ensure services are restarted even if backup fails (replaces bash `restore_current` pattern).

**Module Record:**
```csharp
public sealed record GuardModule(
    ModuleContext Body,             // Primary module to execute
    ModuleContext Finally,          // Always executed after body (success or failure)
    ModuleContext? OnError          // Optional: executed only on error, before finally
) : IModule
{
    public static string ModuleId => "cyborg.modules.guard.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.guard.v1": {
    "body": {
      "module": {
        "cyborg.modules.sequence.v1": {
          "steps": [
            { "module": { "cyborg.modules.docker.down.v1": { ... } } },
            { "module": { "cyborg.modules.borg.create.v1": { ... } } }
          ]
        }
      }
    },
    "on_error": {
      "module": { "cyborg.modules.log.error.v1": { "message": "Backup failed" } }
    },
    "finally": {
      "module": { "cyborg.modules.docker.up.v1": { ... } }
    }
  }
}
```

**Worker Behavior:**
```csharp
try
{
    bool bodyResult = await runtime.ExecuteAsync(Module.Body, cancellationToken);
    if (!bodyResult && Module.OnError is not null)
    {
        await runtime.ExecuteAsync(Module.OnError, CancellationToken.None);
    }
    return bodyResult;
}
finally
{
    // Always execute cleanup, even on cancellation
    await runtime.ExecuteAsync(Module.Finally, CancellationToken.None);
}
```

---

### Conditional Module (`cyborg.modules.if.v1`)

Conditionally executes modules based on environment variable values.

**Purpose:** Handle optional steps (e.g., skip backup if directory doesn't exist).

**Module Record:**
```csharp
public sealed record ConditionalModule(
    ConditionalExpression Condition,    // Condition to evaluate
    ModuleContext Then,                 // Execute if condition is true
    ModuleContext? Else                 // Optional: execute if condition is false
) : IModule
{
    public static string ModuleId => "cyborg.modules.if.v1";
}

public sealed record ConditionalExpression(
    string Variable,                    // Environment variable to check
    ConditionalOperator Operator,       // Comparison operator
    string? Value                       // Value to compare against (null for exists/not_exists)
);

public enum ConditionalOperator
{
    Equals,
    NotEquals,
    Exists,
    NotExists,
    IsTrue,
    IsFalse
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.if.v1": {
    "condition": {
      "variable": "smb_root_exists",
      "operator": "is_true"
    },
    "then": {
      "module": { "cyborg.modules.borg.create.v1": { ... } }
    },
    "else": {
      "module": { "cyborg.modules.log.warn.v1": { "message": "Skipping: directory not found" } }
    }
  }
}
```

---

## Borg Modules

### Repository Configuration Module (`cyborg.modules.borg.repository.v1`)

Defines borg repository connection parameters for use by child modules.

**Purpose:** Centralize repository configuration, inject `BORG_REPO` and `BORG_RSH` environment variables.

**Module Record:**
```csharp
public sealed record BorgRepositoryModule(
    string Hostname,                    // Remote host (e.g., "backup1.service.home.arpa")
    int Port,                           // SSH port (e.g., 12322)
    string RepositoryRoot,              // Base path on remote (e.g., "/var/backups/borg/nas1")
    string RepositoryName,              // Repository name (e.g., "overleaf")
    string? SshCommand,                 // Custom SSH command (for sshpass)
    string? Passphrase,                 // Repository passphrase (from secrets)
    ModuleContext Body                  // Child module(s) to execute with this repository
) : IModule
{
    public static string ModuleId => "cyborg.modules.borg.repository.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.borg.repository.v1": {
    "hostname": "${current_host.hostname}",
    "port": "${current_host.port}",
    "repository_root": "${current_host.borg_repo_root}",
    "repository_name": "overleaf",
    "ssh_command": "${current_host.borg_rsh}",
    "passphrase": "${secrets.overleaf.passphrase}",
    "body": {
      "module": {
        "cyborg.modules.sequence.v1": {
          "steps": [
            { "module": { "cyborg.modules.borg.create.v1": { ... } } },
            { "module": { "cyborg.modules.borg.prune.v1": { ... } } },
            { "module": { "cyborg.modules.borg.compact.v1": { ... } } }
          ]
        }
      }
    }
  }
}
```

**Worker Behavior:**
1. Construct `BORG_REPO = ssh://borg@{hostname}:{port}{repository_root}/{repository_name}`
2. Set `BORG_RSH` from `ssh_command`
3. Set `BORG_PASSPHRASE` from `passphrase`
4. Execute `body` in scoped environment with these variables
5. Clear `BORG_PASSPHRASE` on exit (security)

---

### Borg Create Module (`cyborg.modules.borg.create.v1`)

Executes `borg create` to create a backup archive.

**Purpose:** Type-safe borg create invocation with all supported options.

**Module Record:**
```csharp
public sealed record BorgCreateModule(
    string ArchiveNamePattern,                      // e.g., "{name}-{now}" (borg placeholders)
    ImmutableArray<string> Paths,                   // Paths to backup
    BorgCompression? Compression,                   // Compression settings
    ImmutableArray<string> ExcludePatterns,         // --exclude patterns
    bool ExcludeCaches = true,                      // --exclude-caches
    bool ShowStats = true,                          // --stats
    bool ShowRc = true                              // --show-rc
) : IModule
{
    public static string ModuleId => "cyborg.modules.borg.create.v1";
}

public sealed record BorgCompression(
    BorgCompressionAlgorithm Algorithm,
    int? Level                                      // Optional compression level
);

public enum BorgCompressionAlgorithm { None, Lz4, Zstd, Zlib, Lzma }
```

**JSON Configuration:**
```json
{
  "cyborg.modules.borg.create.v1": {
    "archive_name_pattern": "overleaf-{now}",
    "paths": [ "/opt/docker/volumes/overleaf" ],
    "compression": {
      "algorithm": "zlib"
    },
    "exclude_patterns": [
      "*/mongo/diagnostic.data/*",
      "*/sharelatex/tmp/*",
      "*/sharelatex/data/cache/*"
    ],
    "exclude_caches": true,
    "show_stats": true,
    "show_rc": true
  }
}
```

**Worker Behavior:**
1. Resolve `BORG_REPO`, `BORG_RSH`, `BORG_PASSPHRASE` from environment (set by repository module)
2. Build argument list programmatically (no string interpolation):
   ```csharp
   ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();
   args.Add("create");
   if (Module.ShowRc) args.Add("--show-rc");
   if (Module.ShowStats) args.Add("--stats");
   if (Module.Compression is { } c)
   {
       args.Add("--compression");
       args.Add(c.Level.HasValue ? $"{c.Algorithm.ToLower()},{c.Level}" : c.Algorithm.ToLower());
   }
   // ... add excludes, paths
   args.Add($"::{Module.ArchiveNamePattern}");
   args.AddRange(Module.Paths);
   ```
3. Execute subprocess with environment variables
4. Parse stdout for metrics (via grammar-based parser)
5. Record metrics: `cyborg_borg_create_duration_seconds`, `cyborg_borg_archive_size_bytes`, etc.

**Security:** No shell expansion occurs. Arguments are passed directly to `Process.StartInfo.Arguments` as an array, preventing injection.

---

### Borg Prune Module (`cyborg.modules.borg.prune.v1`)

Executes `borg prune` to remove old archives.

**Module Record:**
```csharp
public sealed record BorgPruneModule(
    string GlobArchives,                            // --glob-archives pattern
    BorgRetentionPolicy Retention,                  // Retention settings
    bool ShowList = true,                           // --list
    bool ShowRc = true                              // --show-rc
) : IModule
{
    public static string ModuleId => "cyborg.modules.borg.prune.v1";
}

public sealed record BorgRetentionPolicy(
    int? KeepDaily,
    int? KeepWeekly,
    int? KeepMonthly,
    int? KeepYearly
);
```

**JSON Configuration:**
```json
{
  "cyborg.modules.borg.prune.v1": {
    "glob_archives": "overleaf-*",
    "retention": {
      "keep_daily": 30,
      "keep_weekly": 12,
      "keep_monthly": 12
    },
    "show_list": true,
    "show_rc": true
  }
}
```

---

### Borg Compact Module (`cyborg.modules.borg.compact.v1`)

Executes `borg compact` to reclaim disk space.

**Module Record:**
```csharp
public sealed record BorgCompactModule(
    bool ShowRc = true                              // --show-rc
) : IModule
{
    public static string ModuleId => "cyborg.modules.borg.compact.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.borg.compact.v1": {
    "show_rc": true
  }
}
```

---

## Service Management Modules

### Docker Compose Down Module (`cyborg.modules.docker.down.v1`)

Stops a Docker Compose stack.

**Purpose:** Replace `docker_down()` bash function with type-safe subprocess invocation.

**Module Record:**
```csharp
public sealed record DockerComposeDownModule(
    string ComposePath,                             // Path to docker-compose.yml
    string? User,                                   // Run as this user (su -c)
    int TimeoutSeconds = 60                         // Timeout waiting for containers to stop
) : IModule
{
    public static string ModuleId => "cyborg.modules.docker.down.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.docker.down.v1": {
    "compose_path": "/opt/docker/containers/overleaf/docker-compose.yml",
    "user": "docker",
    "timeout_seconds": 60
  }
}
```

**Worker Behavior:**
1. Validate compose file exists (fail early, don't invoke subprocess)
2. Build command: `["docker", "compose", "-f", composePath, "down"]`
3. If `user` specified: prepend user switching (via `su` or capability-based approach)
4. Execute subprocess with timeout
5. Return success/failure based on exit code

**Security Note:** The `user` field is validated against a whitelist of allowed users defined in global configuration, preventing privilege escalation to arbitrary users.

---

### Docker Compose Up Module (`cyborg.modules.docker.up.v1`)

Starts a Docker Compose stack.

**Module Record:**
```csharp
public sealed record DockerComposeUpModule(
    string ComposePath,
    string? User,
    bool Detached = true,                           // -d flag
    int TimeoutSeconds = 120
) : IModule
{
    public static string ModuleId => "cyborg.modules.docker.up.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.docker.up.v1": {
    "compose_path": "/opt/docker/containers/overleaf/docker-compose.yml",
    "user": "docker",
    "detached": true
  }
}
```

---

### Systemd Start Module (`cyborg.modules.systemd.start.v1`)

Starts a systemd service.

**Module Record:**
```csharp
public sealed record SystemdStartModule(
    string ServiceName                              // Service unit name (e.g., "smbd.service")
) : IModule
{
    public static string ModuleId => "cyborg.modules.systemd.start.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.systemd.start.v1": {
    "service_name": "smbd.service"
  }
}
```

**Worker Behavior:**
1. Validate service name matches pattern `^[a-zA-Z0-9_-]+\.service$` (prevent injection)
2. Execute: `systemctl start {serviceName}`
3. Return based on exit code

---

### Systemd Stop Module (`cyborg.modules.systemd.stop.v1`)

Stops a systemd service.

**Module Record:**
```csharp
public sealed record SystemdStopModule(
    string ServiceName
) : IModule
{
    public static string ModuleId => "cyborg.modules.systemd.stop.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.systemd.stop.v1": {
    "service_name": "smbd.service"
  }
}
```

---

## Network Modules

### Wake-on-LAN Module (`cyborg.modules.wol.wake.v1`)

Wakes a remote host via Wake-on-LAN and waits for SSH availability.

**Purpose:** Replace `borg_poke_backup_host()` function.

**Module Record:**
```csharp
public sealed record WakeOnLanModule(
    string Hostname,                                // Target hostname (for DNS resolution)
    string MacAddress,                              // WoL MAC address
    int SshPort,                                    // Port to probe for SSH readiness
    int TimeoutSeconds = 240,                       // Max wait time
    int PollIntervalSeconds = 2,                    // Time between SSH probes
    string? StateVariable                           // Optional: store WoL state for cleanup
) : IModule
{
    public static string ModuleId => "cyborg.modules.wol.wake.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.wol.wake.v1": {
    "hostname": "backup1.service.home.arpa",
    "mac_address": "74:46:a0:a1:b6:3c",
    "ssh_port": 12322,
    "timeout_seconds": 240,
    "state_variable": "wol_state.backup1"
  }
}
```

**Worker Behavior:**
1. Ping host to check current state
2. If unreachable:
   a. Resolve hostname to IP
   b. Send WoL magic packet via `wakeonlan` subprocess
   c. Poll SSH port until reachable or timeout
   d. Set `state_variable = "woken"` in environment
3. If already reachable:
   a. Set `state_variable = "was_up"` in environment
4. Return success/failure

**Metrics:**
- `cyborg_wol_wake_total{host}` - Counter of WoL attempts
- `cyborg_wol_wake_duration_seconds{host}` - Time to wake host

---

### SSH Shutdown Module (`cyborg.modules.ssh.shutdown.v1`)

Shuts down a remote host via SSH (for cleanup after WoL).

**Module Record:**
```csharp
public sealed record SshShutdownModule(
    string Hostname,
    int Port,
    string? SshCommand,                             // Custom SSH command (sshpass wrapper)
    string? StateVariable,                          // Only shutdown if state was "woken"
    bool Force = false                              // Shutdown even if state is "was_up"
) : IModule
{
    public static string ModuleId => "cyborg.modules.ssh.shutdown.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.ssh.shutdown.v1": {
    "hostname": "backup1.service.home.arpa",
    "port": 12322,
    "ssh_command": "/usr/bin/sshpass -f/root/.ssh/pass -P assphrase /usr/bin/ssh",
    "state_variable": "wol_state.backup1"
  }
}
```

**Worker Behavior:**
1. If `state_variable` is set and value is not `"woken"`, skip (return success)
2. Execute: `{ssh_command} root@{hostname} '/usr/bin/shutdown -h now'`
3. Return success (shutdown won't return)

---

### Network Ping Module (`cyborg.modules.net.ping.v1`)

Checks if a host is reachable via ICMP ping.

**Module Record:**
```csharp
public sealed record NetworkPingModule(
    string Hostname,
    int TimeoutSeconds = 10,
    int Count = 1,
    string? ResultVariable                          // Store result in environment
) : IModule
{
    public static string ModuleId => "cyborg.modules.net.ping.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.net.ping.v1": {
    "hostname": "backup1.service.home.arpa",
    "timeout_seconds": 10,
    "result_variable": "host_reachable"
  }
}
```

---

## Security Modules

### Secrets Load Module (`cyborg.modules.secrets.load.v1`)

Loads secrets from a secure source into the runtime environment.

**Purpose:** Replace bash `.secrets` file sourcing with secure, structured secret loading.

**Module Record:**
```csharp
public sealed record SecretsLoadModule(
    string Source,                                  // Secret source identifier
    SecretsSourceType SourceType,                   // How to load secrets
    string EnvironmentPrefix,                       // Prefix for environment variables
    ImmutableArray<string>? Keys                    // Specific keys to load (null = all)
) : IModule
{
    public static string ModuleId => "cyborg.modules.secrets.load.v1";
}

public enum SecretsSourceType
{
    EnvironmentVariable,                            // From process environment
    JsonFile,                                       // From JSON file (encrypted or not)
    SystemdCredential                               // From systemd credentials API
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.secrets.load.v1": {
    "source": "overleaf-secrets",
    "source_type": "systemd_credential",
    "environment_prefix": "secrets.overleaf",
    "keys": [ "passphrase" ]
  }
}
```

**Worker Behavior:**
1. Load secret(s) from specified source
2. Set environment variables: `{prefix}.{key}` = value
3. Register cleanup handler to clear secrets from environment on scope exit

**Supported Sources (extensible):**
- `EnvironmentVariable` - Read from process environment (for Kubernetes/container secrets)
- `JsonFile` - Read from JSON file (can be encrypted with age/sops)
- `SystemdCredential` - Use systemd's credentials mechanism for services

---

## System Modules

### Run-As Module (`cyborg.modules.system.run_as.v1`)

Executes child modules as a different user.

**Purpose:** Replace `su -c` patterns for running Docker as non-root user.

**Module Record:**
```csharp
public sealed record RunAsModule(
    string User,                                    // Target user
    ModuleContext Body                              // Module to execute as user
) : IModule
{
    public static string ModuleId => "cyborg.modules.system.run_as.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.system.run_as.v1": {
    "user": "docker",
    "body": {
      "module": {
        "cyborg.modules.docker.down.v1": {
          "compose_path": "/opt/docker/containers/overleaf/docker-compose.yml"
        }
      }
    }
  }
}
```

**Security Constraints:**
1. `User` must be in allowed users list (global configuration)
2. Implementation uses capability-based switching or `sudo -u` with NOPASSWD for specific commands
3. No arbitrary command execution - only child module commands are allowed

---

## Logging Modules

### Log Module (`cyborg.modules.log.v1`)

Writes a structured log message.

**Module Record:**
```csharp
public sealed record LogModule(
    LogLevel Level,
    string Message,
    ImmutableDictionary<string, string>? Properties
) : IModule
{
    public static string ModuleId => "cyborg.modules.log.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.log.v1": {
    "level": "info",
    "message": "Starting backup for ${container_name}",
    "properties": {
      "host": "${current_host.hostname}"
    }
  }
}
```

---

## Complete Job Configuration Examples

### Example: Overleaf Daily Backup (`jobs/daily/overleaf.json`)

```json
{
  "environment": {
    "scope": "inherit_parent"
  },
  "configuration": {
    "cyborg.modules.config.map.v1": {
      "entries": [
        { "key": "container_name", "string": "overleaf" },
        { "key": "container_root", "string": "/opt/docker/containers/overleaf" },
        { "key": "volume_root", "string": "/opt/docker/volumes/overleaf" }
      ]
    }
  },
  "module": {
    "cyborg.modules.secrets.load.v1": {
      "source": "overleaf-secrets",
      "source_type": "systemd_credential",
      "environment_prefix": "secrets",
      "body": {
        "module": {
          "cyborg.modules.guard.v1": {
            "body": {
              "module": {
                "cyborg.modules.sequence.v1": {
                  "steps": [
                    {
                      "module": {
                        "cyborg.modules.system.run_as.v1": {
                          "user": "docker",
                          "body": {
                            "module": {
                              "cyborg.modules.docker.down.v1": {
                                "compose_path": "${container_root}/docker-compose.yml"
                              }
                            }
                          }
                        }
                      }
                    },
                    {
                      "module": {
                        "cyborg.modules.log.v1": {
                          "level": "info",
                          "message": "Starting backup for ${container_name}"
                        }
                      }
                    },
                    {
                      "module": {
                        "cyborg.modules.foreach.v1": {
                          "collection": "backup_hosts",
                          "item_variable": "host",
                          "body": {
                            "module": {
                              "cyborg.modules.borg.repository.v1": {
                                "hostname": "${host.hostname}",
                                "port": "${host.port}",
                                "repository_root": "${host.borg_repo_root}",
                                "repository_name": "${container_name}",
                                "ssh_command": "${host.borg_rsh}",
                                "passphrase": "${secrets.passphrase}",
                                "body": {
                                  "module": {
                                    "cyborg.modules.sequence.v1": {
                                      "steps": [
                                        {
                                          "module": {
                                            "cyborg.modules.borg.create.v1": {
                                              "archive_name_pattern": "${container_name}-{now}",
                                              "paths": [ "${volume_root}" ],
                                              "compression": { "algorithm": "zlib" },
                                              "exclude_patterns": [
                                                "*/mongo/diagnostic.data/*",
                                                "*/sharelatex/tmp/*",
                                                "*/sharelatex/data/cache/*",
                                                "*/sharelatex/data/compiles/*",
                                                "*/sharelatex/data/output/*"
                                              ],
                                              "exclude_caches": true
                                            }
                                          }
                                        },
                                        {
                                          "module": {
                                            "cyborg.modules.borg.prune.v1": {
                                              "glob_archives": "${container_name}-*",
                                              "retention": {
                                                "keep_daily": 30,
                                                "keep_weekly": 12,
                                                "keep_monthly": 12
                                              }
                                            }
                                          }
                                        },
                                        {
                                          "module": {
                                            "cyborg.modules.borg.compact.v1": {}
                                          }
                                        }
                                      ]
                                    }
                                  }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  ]
                }
              }
            },
            "finally": {
              "module": {
                "cyborg.modules.system.run_as.v1": {
                  "user": "docker",
                  "body": {
                    "module": {
                      "cyborg.modules.docker.up.v1": {
                        "compose_path": "${container_root}/docker-compose.yml"
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}
```

### Example: SMB Data Daily Backup (`jobs/daily/smb-data.json`)

```json
{
  "module": {
    "cyborg.modules.guard.v1": {
      "body": {
        "module": {
          "cyborg.modules.sequence.v1": {
            "steps": [
              {
                "module": {
                  "cyborg.modules.systemd.stop.v1": {
                    "service_name": "smbd.service"
                  }
                }
              },
              {
                "module": {
                  "cyborg.modules.foreach.v1": {
                    "collection": "smb_targets",
                    "item_variable": "target",
                    "continue_on_error": false,
                    "body": {
                      "module": {
                        "cyborg.modules.sequence.v1": {
                          "steps": [
                            {
                              "module": {
                                "cyborg.modules.secrets.load.v1": {
                                  "source": "${target.secrets_file}",
                                  "source_type": "json_file",
                                  "environment_prefix": "secrets"
                                }
                              }
                            },
                            {
                              "module": {
                                "cyborg.modules.if.v1": {
                                  "condition": {
                                    "variable": "target.root",
                                    "operator": "exists"
                                  },
                                  "then": {
                                    "module": {
                                      "cyborg.modules.foreach.v1": {
                                        "collection": "backup_hosts",
                                        "item_variable": "host",
                                        "body": {
                                          "module": {
                                            "cyborg.modules.borg.repository.v1": {
                                              "hostname": "${host.hostname}",
                                              "port": "${host.port}",
                                              "repository_root": "${host.borg_repo_root}",
                                              "repository_name": "${target.repository_name}",
                                              "ssh_command": "${host.borg_rsh}",
                                              "passphrase": "${secrets.passphrase}",
                                              "body": {
                                                "module": {
                                                  "cyborg.modules.sequence.v1": {
                                                    "steps": [
                                                      {
                                                        "module": {
                                                          "cyborg.modules.borg.create.v1": {
                                                            "archive_name_pattern": "${target.repository_name}-{now}",
                                                            "paths": [ "${target.root}" ],
                                                            "compression": { "algorithm": "zlib" },
                                                            "exclude_caches": true
                                                          }
                                                        }
                                                      },
                                                      {
                                                        "module": {
                                                          "cyborg.modules.borg.prune.v1": {
                                                            "glob_archives": "${target.repository_name}-*",
                                                            "retention": {
                                                              "keep_daily": 30,
                                                              "keep_weekly": 24,
                                                              "keep_monthly": 24
                                                            }
                                                          }
                                                        }
                                                      },
                                                      {
                                                        "module": {
                                                          "cyborg.modules.borg.compact.v1": {}
                                                        }
                                                      }
                                                    ]
                                                  }
                                                }
                                              }
                                            }
                                          }
                                        }
                                      }
                                    }
                                  },
                                  "else": {
                                    "module": {
                                      "cyborg.modules.log.v1": {
                                        "level": "warn",
                                        "message": "SMB root '${target.root}' does not exist, skipping"
                                      }
                                    }
                                  }
                                }
                              }
                            }
                          ]
                        }
                      }
                    }
                  }
                }
              }
            ]
          }
        }
      },
      "finally": {
        "module": {
          "cyborg.modules.systemd.start.v1": {
            "service_name": "smbd.service"
          }
        }
      }
    }
  }
}
```

---

## Global Configuration (`config.json`)

```json
{
  "configuration": {
    "cyborg.modules.config.map.v1": {
      "entries": [
        {
          "key": "backup_hosts",
          "collection": [
            {
              "hostname": "backup1.service.home.arpa",
              "port": 12322,
              "wake_on_lan_mac": "74:46:a0:a1:b6:3c",
              "borg_rsh": "/usr/bin/sshpass -f/root/.ssh/pass -P assphrase /usr/bin/ssh",
              "borg_repo_root": "/var/backups/borg/nas1.dmz.home.arpa"
            },
            {
              "hostname": "backup2.service.home.arpa",
              "port": 12322,
              "wake_on_lan_mac": "f4:4d:30:45:70:37",
              "borg_rsh": "/usr/bin/sshpass -f/root/.ssh/pass -P assphrase /usr/bin/ssh",
              "borg_repo_root": "/var/backups/borg/nas1.service.home.arpa"
            }
          ]
        },
        {
          "key": "allowed_run_as_users",
          "collection": [ "docker", "backup" ]
        }
      ]
    }
  },
  "module": {
    "cyborg.modules.sequence.v1": {
      "steps": [
        {
          "module": {
            "cyborg.modules.foreach.v1": {
              "collection": "backup_hosts",
              "item_variable": "host",
              "body": {
                "module": {
                  "cyborg.modules.wol.wake.v1": {
                    "hostname": "${host.hostname}",
                    "mac_address": "${host.wake_on_lan_mac}",
                    "ssh_port": "${host.port}",
                    "state_variable": "wol_state.${host.hostname}"
                  }
                }
              }
            }
          }
        },
        {
          "module": {
            "cyborg.modules.template.v1": {
              "templates": [
                { "name": "daily", "path": "daily.json" },
                { "name": "weekly", "path": "weekly.json" },
                { "name": "monthly", "path": "monthly.json" }
              ]
            }
          }
        },
        {
          "module": {
            "cyborg.modules.foreach.v1": {
              "collection": "backup_hosts",
              "item_variable": "host",
              "body": {
                "module": {
                  "cyborg.modules.ssh.shutdown.v1": {
                    "hostname": "${host.hostname}",
                    "port": "${host.port}",
                    "ssh_command": "${host.borg_rsh}",
                    "state_variable": "wol_state.${host.hostname}"
                  }
                }
              }
            }
          }
        }
      ]
    }
  }
}
```

---

## Implementation Priorities

### Phase 1: Core Control Flow (Required for any job)

1. `cyborg.modules.foreach.v1` - Iterate over hosts
2. `cyborg.modules.guard.v1` - Try-finally semantics
3. `cyborg.modules.if.v1` - Conditional execution
4. `cyborg.modules.log.v1` - Logging

### Phase 2: Borg Operations (Core backup functionality)

5. `cyborg.modules.borg.repository.v1` - Repository context
6. `cyborg.modules.borg.create.v1` - Create archives
7. `cyborg.modules.borg.prune.v1` - Prune old archives
8. `cyborg.modules.borg.compact.v1` - Compact repository

### Phase 3: Service Management (Required for existing jobs)

9. `cyborg.modules.docker.down.v1` - Stop Docker
10. `cyborg.modules.docker.up.v1` - Start Docker
11. `cyborg.modules.systemd.start.v1` - Start systemd
12. `cyborg.modules.systemd.stop.v1` - Stop systemd
13. `cyborg.modules.system.run_as.v1` - User switching

### Phase 4: Network Operations (Required for multi-host)

14. `cyborg.modules.wol.wake.v1` - Wake-on-LAN
15. `cyborg.modules.ssh.shutdown.v1` - Remote shutdown
16. `cyborg.modules.net.ping.v1` - Host reachability

### Phase 5: Security & Observability

17. `cyborg.modules.secrets.load.v1` - Secret management
18. Prometheus metrics integration for all modules
19. Borg output parsing grammars

---

## Security Design Principles

### No Shell Expansion

All subprocess invocations use `Process.StartInfo.ArgumentList` (or equivalent array-based API), never string concatenation. This prevents:
- Command injection via `$()` or backticks
- Argument injection via `;`, `&&`, `||`
- Glob expansion vulnerabilities

### Input Validation

Each module validates inputs at deserialization time:
- `ServiceName` matches `^[a-zA-Z0-9_-]+\.service$`
- `MacAddress` matches MAC address format
- `Hostname` validated via DNS resolution or IP format
- File paths validated to exist before use

### Privilege Boundaries

- `run_as.user` validated against `allowed_run_as_users` list
- No arbitrary user execution
- Subprocess capabilities restricted to minimum required

### Secret Handling

- Secrets loaded into scoped environments, cleared on scope exit
- No secrets logged or exported
- Support for encrypted secret files (future: age, sops integration)

---

## Metrics Schema

All metrics use `cyborg_` prefix and follow Prometheus conventions:

### Backup Metrics

```
# Archive creation
cyborg_borg_create_duration_seconds{job, repository, host}
cyborg_borg_create_success_total{job, repository, host}
cyborg_borg_create_failure_total{job, repository, host}
cyborg_borg_archive_original_size_bytes{job, repository}
cyborg_borg_archive_compressed_size_bytes{job, repository}
cyborg_borg_archive_deduplicated_size_bytes{job, repository}

# Pruning
cyborg_borg_prune_archives_kept_total{job, repository}
cyborg_borg_prune_archives_deleted_total{job, repository}

# Repository
cyborg_borg_repo_total_size_bytes{repository}
cyborg_borg_repo_total_unique_chunks{repository}
```

### Infrastructure Metrics

```
# Wake-on-LAN
cyborg_wol_wake_duration_seconds{host}
cyborg_wol_wake_total{host, result}

# Service management
cyborg_service_stop_duration_seconds{service}
cyborg_service_start_duration_seconds{service}

# Job execution
cyborg_job_duration_seconds{job, frequency}
cyborg_job_success_total{job, frequency}
cyborg_job_failure_total{job, frequency}
```

---

## Parser Grammar Requirements

Borg output parsing grammars needed to extract metrics:

### `borg create --stats` Output

```
Archive name: overleaf-2024-01-15T02:00:05
Archive fingerprint: abc123...
Time (start): Mon, 2024-01-15 02:00:05
Time (end):   Mon, 2024-01-15 02:15:32
Duration: 15 minutes 27 seconds
Number of files: 42103
Utilization of max. archive size: 0%
------------------------------------------------------------------------------
                       Original size      Compressed size    Deduplicated size
This archive:               12.45 GB              8.32 GB              1.23 GB
All archives:              156.78 GB            104.52 GB             23.45 GB
------------------------------------------------------------------------------
```

**Grammar:**
```
BorgCreateStats ::= 
  "Archive name:" ArchiveName Newline
  "Archive fingerprint:" Fingerprint Newline
  ... (timestamps)
  "Duration:" Duration Newline
  "Number of files:" FileCount Newline
  ... (separator)
  "This archive:" Size Size Size Newline
  "All archives:" Size Size Size Newline
```

### `borg prune --list` Output

```
Keeping archive: overleaf-2024-01-15T02:00:05
Pruning archive: overleaf-2024-01-01T02:00:05
```

**Grammar:**
```
BorgPruneEntry ::=
  ("Keeping" | "Pruning") "archive:" ArchiveName Newline
```

---

## Extension Points

The architecture supports future extensions:

1. **New borg commands** - Add `borg.info.v1`, `borg.list.v1`, `borg.check.v1` modules
2. **Alternative backends** - Restic support via parallel module implementations
3. **Cloud secrets** - AWS Secrets Manager, Azure Key Vault loaders
4. **Notifications** - Slack, email, webhook modules for job status
5. **Scheduling** - Potential integration with systemd timers or built-in scheduler