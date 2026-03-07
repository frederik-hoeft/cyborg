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

## Existing Runtime Infrastructure

The POC already provides a sophisticated environment and scoping system that the migration design must leverage, not duplicate.

### Environment Scoping (`Cyborg.Core.Modules.Runtime`)

The `EnvironmentScope` enum defines how child modules inherit or share state:

| Scope | Behavior | Use Case |
|-------|----------|----------|
| `Isolated` | Fresh environment, no variable inheritance | Sandboxed execution |
| `Global` | Execute in global singleton environment | Cross-job shared state |
| `InheritParent` | New environment inheriting from immediate parent | Typical module execution |
| `InheritGlobal` | New environment inheriting only from global | Skip parent overrides |
| `Parent` | Share parent's environment directly (no copy) | In-place variable mutation |
| `Reference` | Reference an existing named environment by name | Reuse previously created scope |

**Implementation classes:**
- `RuntimeEnvironment` - Base environment with `Dictionary<string, object?>` storage
- `InheritedRuntimeEnvironment` - Chains to parent for variable resolution fallback
- `GlobalRuntimeEnvironment` - Singleton for global state
- `ScopedRuntime` - Wraps `IModuleRuntime` with a specific environment

### Variable Resolution

`RuntimeEnvironment.TryResolveVariable<T>()` supports:
1. **Direct lookup** - Variable name maps to stored value
2. **Indirection** - String values starting with `$` or `${...}` resolve recursively
3. **Type casting** - Generic `T` constraint ensures type-safe retrieval

```csharp
// Example: "${job_name}" in config resolves through indirection  
if (objValue is string s && s is ['$', ..] && VariableRegex.Match(s) is { Success: true } match)
{
    string variableName = match.Groups["variable_name"].Value;
    return TryResolveVariable(variableName, out value);
}
```

### Named Environment Persistence

Non-transient environments (those with explicit `Name` in JSON config) are registered with the root runtime via `TryAddEnvironment()` and can be retrieved later via `TryGetEnvironment()`. This enables:
- **Cross-step state sharing** - Step 1 creates named environment, Step 3 references it
- **Cleanup scopes** - Finally block references the same environment as try block

### Configuration Loading (`ModuleContext`)

Each module invocation is wrapped in `ModuleContext`:
```csharp
record ModuleContext(
    ModuleReference Module,           // The actual module to execute
    ModuleEnvironment? Environment,   // Scope configuration
    ModuleReference? Configuration    // Optional: ConfigMap/ConfigCollection to populate environment
);
```

The `Configuration` module executes first, populating the environment, before the main `Module` executes.

### Dynamic Value Providers

The `IDynamicValueProvider` system enables type-safe JSON deserialization for config values:
- `string`, `bool`, `int`, `long`, `double`, `decimal`, etc.
- Extensible via additional provider registrations

**JSON syntax:**
```json
{ "key": "max_retries", "int": 3 }
{ "key": "enable_compression", "bool": true }
{ "key": "container_name", "string": "overleaf" }
```

---

## Design Principles for New Modules

### Leverage Existing Scoping

New modules should use `ModuleContext.Environment` for scoping, not custom solutions:

```json
{
  "environment": {
    "scope": "inherit_parent",
    "name": "borg_session"
  },
  "configuration": {
    "cyborg.modules.config.map.v1": {
      "entries": [
        { "key": "repository_name", "string": "overleaf" }
      ]
    }
  },
  "module": { "cyborg.modules.borg.create.v1": { ... } }
}
```

### Use `IModuleRuntime` for Child Execution

Modules that execute children should use the runtime's execution methods:
```csharp
// Execute child with inherited environment
await runtime.ExecuteAsync(childContext, cancellationToken);

// Execute child with explicit scope
await runtime.ExecuteAsync(childWorker, EnvironmentScope.InheritParent, "child_scope", cancellationToken);

// Execute child in same environment (no scope change)
await runtime.ExecuteAsync(childWorker, runtime.Environment, cancellationToken);
```

### Read Variables via Environment

Modules read configuration from the environment rather than through constructor parameters:
```csharp
// In module worker:
if (!runtime.Environment.TryResolveVariable("borg_passphrase", out string? passphrase))
{
    throw new InvalidOperationException("Missing required variable: borg_passphrase");
}
```

This allows late-binding and indirection (`${variable}` syntax).

---

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
    ModuleContext Body,             // Module to execute for each item (includes its own environment config)
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
      "environment": {
        "scope": "inherit_parent",
        "name": "host_iteration"
      },
      "module": { "cyborg.modules.borg.create.v1": { ... } }
    }
  }
}
```

**Worker Behavior:**
```csharp
protected override async Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
{
    // Resolve collection from current environment
    if (!runtime.Environment.TryResolveVariable<IEnumerable<object>>(Module.Collection, out var items))
    {
        throw new InvalidOperationException($"Collection '{Module.Collection}' not found in environment.");
    }
    
    int index = 0;
    foreach (object item in items)
    {
        // Create iteration scope that inherits from current environment
        // Body's ModuleContext.Environment controls child scoping
        // We inject item/index into the scope used by body
        
        // Resolve body's environment scope (default to InheritParent)
        EnvironmentScope bodyScope = Module.Body.Environment?.Scope ?? EnvironmentScope.InheritParent;
        string? bodyName = Module.Body.Environment?.Name;
        
        IRuntimeEnvironment iterationEnv = CreateIterationEnvironment(runtime, bodyScope, bodyName);
        iterationEnv.SetVariable(Module.ItemVariable, item);
        if (Module.IndexVariable is not null)
        {
            iterationEnv.SetVariable(Module.IndexVariable, index);
        }
        
        // Execute body's configuration module (if any) then body module
        bool success = await runtime.ExecuteAsync(Module.Body.Module.Module, iterationEnv, cancellationToken);
        
        if (!success && !Module.ContinueOnError)
        {
            return false;
        }
        index++;
    }
    return true;
}
```

**Scoping Integration:**
- For each iteration, creates a new environment based on `body.environment.scope`
- Item and index variables are set in the iteration environment
- Child module inherits from iteration environment for variable resolution
- Named environments (`body.environment.name`) are registered for later `Reference` scope usage

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

**JSON Configuration (with scoping):**
```json
{
  "cyborg.modules.guard.v1": {
    "body": {
      "environment": {
        "scope": "inherit_parent",
        "name": "backup_session"
      },
      "configuration": {
        "cyborg.modules.config.map.v1": {
          "entries": [
            { "key": "container_name", "string": "overleaf" }
          ]
        }
      },
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
      "environment": {
        "scope": "reference",
        "name": "backup_session"
      },
      "module": { "cyborg.modules.log.error.v1": { "message": "Backup failed for ${container_name}" } }
    },
    "finally": {
      "environment": {
        "scope": "reference",
        "name": "backup_session"
      },
      "module": { "cyborg.modules.docker.up.v1": { ... } }
    }
  }
}
```

**Key Scoping Pattern:**
- `body` creates a named environment (`"backup_session"`) with `inherit_parent` scope
- `on_error` and `finally` use `reference` scope to access the same environment
- This allows `finally` to read variables set during `body` execution (e.g., `container_name`)

**Worker Behavior:**
```csharp
protected override async Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
{
    bool bodyResult = false;
    try
    {
        bodyResult = await runtime.ExecuteAsync(Module.Body, cancellationToken);
        if (!bodyResult && Module.OnError is not null)
        {
            // OnError can reference body's named environment
            await runtime.ExecuteAsync(Module.OnError, CancellationToken.None);
        }
        return bodyResult;
    }
    finally
    {
        // Finally always executes, even on cancellation
        // Uses CancellationToken.None to ensure cleanup completes
        await runtime.ExecuteAsync(Module.Finally, CancellationToken.None);
        
        // Optionally clean up named environment if transient
        // (handled automatically by runtime for transient envs)
    }
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
      "environment": { "scope": "parent" },
      "module": { "cyborg.modules.borg.create.v1": { ... } }
    },
    "else": {
      "environment": { "scope": "parent" },
      "module": { "cyborg.modules.log.warn.v1": { "message": "Skipping: directory not found" } }
    }
  }
}
```

**Worker Behavior:**
```csharp
protected override async Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
{
    bool conditionMet = EvaluateCondition(runtime.Environment, Module.Condition);
    
    if (conditionMet)
    {
        return await runtime.ExecuteAsync(Module.Then, cancellationToken);
    }
    else if (Module.Else is not null)
    {
        return await runtime.ExecuteAsync(Module.Else, cancellationToken);
    }
    return true; // No else branch, condition not met = success
}

private static bool EvaluateCondition(IRuntimeEnvironment env, ConditionalExpression condition)
{
    bool exists = env.TryResolveVariable<object>(condition.Variable, out object? value);
    
    return condition.Operator switch
    {
        ConditionalOperator.Exists => exists,
        ConditionalOperator.NotExists => !exists,
        ConditionalOperator.IsTrue => exists && value is true or "true" or "1",
        ConditionalOperator.IsFalse => !exists || value is false or "false" or "0",
        ConditionalOperator.Equals => exists && value?.ToString() == condition.Value,
        ConditionalOperator.NotEquals => !exists || value?.ToString() != condition.Value,
        _ => throw new ArgumentOutOfRangeException()
    };
}
```

---

## Borg Modules

### Repository Configuration Module (`cyborg.modules.borg.repository.v1`)

Sets up borg repository environment variables for child modules. Uses standard environment scoping.

**Purpose:** Centralize repository configuration, inject `BORG_REPO` and `BORG_RSH` environment variables for child modules.

**Module Record:**
```csharp
public sealed record BorgRepositoryModule(
    string Hostname,                    // Remote host (e.g., "backup1.service.home.arpa")
    int Port,                           // SSH port (e.g., 12322)
    string RepositoryRoot,              // Base path on remote (e.g., "/var/backups/borg/nas1")
    string RepositoryName,              // Repository name (e.g., "overleaf")
    string? SshCommand,                 // Custom SSH command (for sshpass)
    string? PassphraseVariable,         // Environment variable containing passphrase
    ModuleContext Body                  // Child module(s) to execute with this repository
) : IModule
{
    public static string ModuleId => "cyborg.modules.borg.repository.v1";
}
```

**JSON Configuration (leveraging scoping):**
```json
{
  "environment": {
    "scope": "inherit_parent",
    "name": "borg_repo_session"
  },
  "configuration": {
    "cyborg.modules.config.map.v1": {
      "entries": [
        { "key": "borg_passphrase", "string": "${secrets.overleaf.passphrase}" }
      ]
    }
  },
  "module": {
    "cyborg.modules.borg.repository.v1": {
      "hostname": "${current_host.hostname}",
      "port": "${current_host.port}",
      "repository_root": "${current_host.borg_repo_root}",
      "repository_name": "${container_name}",
      "ssh_command": "${current_host.borg_rsh}",
      "passphrase_variable": "borg_passphrase",
      "body": {
        "environment": { "scope": "parent" },
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
}
```

**Worker Behavior:**
```csharp
protected override async Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
{
    // Construct BORG_REPO from module properties (which may contain ${var} indirection)
    string hostname = ResolveProperty(runtime.Environment, Module.Hostname);
    int port = int.Parse(ResolveProperty(runtime.Environment, Module.Port.ToString()));
    string repoRoot = ResolveProperty(runtime.Environment, Module.RepositoryRoot);
    string repoName = ResolveProperty(runtime.Environment, Module.RepositoryName);
    
    string borgRepo = $"ssh://borg@{hostname}:{port}{repoRoot}/{repoName}";
    
    // Set borg environment variables in current scope
    runtime.Environment.SetVariable("BORG_REPO", borgRepo);
    
    if (Module.SshCommand is not null)
    {
        string sshCommand = ResolveProperty(runtime.Environment, Module.SshCommand);
        runtime.Environment.SetVariable("BORG_RSH", sshCommand);
    }
    
    if (Module.PassphraseVariable is not null)
    {
        // Passphrase is read from environment, not stored in module
        if (runtime.Environment.TryResolveVariable<string>(Module.PassphraseVariable, out string? passphrase))
        {
            runtime.Environment.SetVariable("BORG_PASSPHRASE", passphrase);
        }
    }
    
    try
    {
        // Body inherits these variables via environment scoping
        return await runtime.ExecuteAsync(Module.Body, cancellationToken);
    }
    finally
    {
        // Security: clear passphrase from environment
        runtime.Environment.TryRemoveVariable("BORG_PASSPHRASE");
    }
}
```

**Environment Flow:**
1. Parent scope sets secrets via `ConfigMapModule` (e.g., `borg_passphrase`)
2. `BorgRepositoryModule` reads passphrase from `passphrase_variable`
3. Module sets `BORG_REPO`, `BORG_RSH`, `BORG_PASSPHRASE` in current environment
4. Body's `scope: parent` shares the same environment
5. Child borg modules (`create`, `prune`, `compact`) read from environment
6. Cleanup removes `BORG_PASSPHRASE`

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
) : IModule, IConfigurationModule                   // Implements IConfigurationModule for use in configuration blocks
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

**JSON Configuration (as configuration module):**

Secrets can be loaded via the `configuration` property of a `ModuleContext`, populating the environment before the main module executes:

```json
{
  "environment": {
    "scope": "inherit_parent",
    "name": "backup_session"
  },
  "configuration": {
    "cyborg.modules.config.collection.v1": {
      "sources": [
        {
          "cyborg.modules.secrets.load.v1": {
            "source": "overleaf-secrets",
            "source_type": "systemd_credential",
            "environment_prefix": "secrets",
            "keys": [ "passphrase" ]
          }
        },
        {
          "cyborg.modules.config.map.v1": {
            "entries": [
              { "key": "container_name", "string": "overleaf" }
            ]
          }
        }
      ]
    }
  },
  "module": { "cyborg.modules.borg.repository.v1": { ... } }
}
```

**Worker Behavior:**
```csharp
protected override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
{
    IEnumerable<KeyValuePair<string, string>> secrets = LoadSecrets(Module.Source, Module.SourceType, Module.Keys);
    
    foreach (KeyValuePair<string, string> secret in secrets)
    {
        string variableName = string.IsNullOrEmpty(Module.EnvironmentPrefix) 
            ? secret.Key 
            : $"{Module.EnvironmentPrefix}.{secret.Key}";
        runtime.Environment.SetVariable(variableName, secret.Value);
    }
    
    return Task.FromResult(true);
}
```

**Environment Integration:**
- Secrets module implements `IConfigurationModule`, allowing use in `configuration` blocks
- Variables are set in the current environment scope
- Subsequent modules (and children via `inherit_parent`) can resolve secrets via `${secrets.passphrase}`
- Transient environments automatically clean up secrets when scope exits

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

## Environment Scoping Patterns for Backup Workflows

This section describes common patterns for leveraging the environment scoping system in backup job configurations.

### Pattern 1: Named Scope with Guard Cleanup

The guard module's `finally` block needs access to variables set during the `body`. Use named environments with `reference` scope:

```json
{
  "cyborg.modules.guard.v1": {
    "body": {
      "environment": {
        "scope": "inherit_parent",
        "name": "backup_session"          // Named, non-transient
      },
      "configuration": {
        "cyborg.modules.config.map.v1": {
          "entries": [
            { "key": "compose_path", "string": "/opt/docker/containers/app/docker-compose.yml" }
          ]
        }
      },
      "module": { ... }
    },
    "finally": {
      "environment": {
        "scope": "reference",             // References existing named scope
        "name": "backup_session"
      },
      "module": {
        "cyborg.modules.docker.up.v1": {
          "compose_path": "${compose_path}"   // Resolves from referenced env
        }
      }
    }
  }
}
```

### Pattern 2: ForEach Iteration Variables

ForEach sets `item_variable` in a child scope that inherits from parent. Child modules can read both iteration variables and parent variables:

```json
{
  "cyborg.modules.foreach.v1": {
    "collection": "backup_hosts",
    "item_variable": "host",              // Set per iteration
    "body": {
      "environment": {
        "scope": "inherit_parent"         // Inherits container_name from parent
      },
      "module": {
        "cyborg.modules.borg.repository.v1": {
          "hostname": "${host.hostname}",         // From iteration
          "repository_name": "${container_name}"  // From parent
        }
      }
    }
  }
}
```

### Pattern 3: Secrets via Configuration Block

Load secrets before the main module executes using the `configuration` property:

```json
{
  "environment": {
    "scope": "inherit_parent",
    "name": "job_scope"
  },
  "configuration": {
    "cyborg.modules.config.collection.v1": {
      "sources": [
        {
          "cyborg.modules.secrets.load.v1": {
            "source": "backup-secrets",
            "source_type": "systemd_credential",
            "environment_prefix": "secrets"
          }
        },
        {
          "cyborg.modules.config.map.v1": {
            "entries": [
              { "key": "container_name", "string": "overleaf" }
            ]
          }
        }
      ]
    }
  },
  "module": {
    "cyborg.modules.borg.repository.v1": {
      "passphrase_variable": "secrets.passphrase"   // Read from config-loaded secret
    }
  }
}
```

### Pattern 4: Scope Isolation for Nested Loops

When nesting foreach loops, use `inherit_parent` to create a scope chain:

```json
{
  "cyborg.modules.foreach.v1": {
    "collection": "smb_targets",
    "item_variable": "target",
    "body": {
      "environment": { "scope": "inherit_parent" },
      "module": {
        "cyborg.modules.foreach.v1": {
          "collection": "backup_hosts",
          "item_variable": "host",
          "body": {
            "environment": { "scope": "inherit_parent" },
            "module": {
              "cyborg.modules.borg.create.v1": {
                "archive_name_pattern": "${target.name}-{now}",  // From outer loop
                "paths": [ "${target.root}" ]                     // From outer loop
              }
            }
          }
        }
      }
    }
  }
}
```

Variable resolution chain: `inner iteration → outer iteration → parent → global`

### Pattern 5: Parent Scope for In-Place Mutation

Use `scope: parent` when a module should modify the calling scope directly:

```json
{
  "cyborg.modules.sequence.v1": {
    "steps": [
      {
        "environment": { "scope": "parent" },
        "module": {
          "cyborg.modules.config.map.v1": {
            "entries": [
              { "key": "step1_result", "string": "computed_value" }
            ]
          }
        }
      },
      {
        "environment": { "scope": "parent" },
        "module": {
          "cyborg.modules.log.v1": {
            "message": "Step 1 set: ${step1_result}"   // Visible because same scope
          }
        }
      }
    ]
  }
}
```

---

## Complete Job Configuration Examples

### Example: Overleaf Daily Backup (`jobs/daily/overleaf.json`)

This example demonstrates:
- Named guard scope for cleanup variable access
- Configuration block for secrets + config loading
- ForEach iteration with inherited scopes
- Repository module setting borg environment variables

```json
{
  "environment": {
    "scope": "inherit_parent",
    "name": "overleaf_job"
  },
  "configuration": {
    "cyborg.modules.config.collection.v1": {
      "sources": [
        {
          "cyborg.modules.secrets.load.v1": {
            "source": "overleaf-secrets",
            "source_type": "systemd_credential",
            "environment_prefix": "secrets"
          }
        },
        {
          "cyborg.modules.config.map.v1": {
            "entries": [
              { "key": "container_name", "string": "overleaf" },
              { "key": "container_root", "string": "/opt/docker/containers/overleaf" },
              { "key": "volume_root", "string": "/opt/docker/volumes/overleaf" },
              { "key": "docker_user", "string": "docker" }
            ]
          }
        }
      ]
    }
  },
  "module": {
    "cyborg.modules.guard.v1": {
      "body": {
        "environment": { "scope": "parent" },
        "module": {
          "cyborg.modules.sequence.v1": {
            "steps": [
              {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.docker.down.v1": {
                    "compose_path": "${container_root}/docker-compose.yml",
                    "user": "${docker_user}"
                  }
                }
              },
              {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.log.v1": {
                    "level": "info",
                    "message": "Starting backup for ${container_name}"
                  }
                }
              },
              {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.foreach.v1": {
                    "collection": "backup_hosts",
                    "item_variable": "host",
                    "body": {
                      "environment": { "scope": "inherit_parent" },
                      "module": {
                        "cyborg.modules.borg.repository.v1": {
                          "hostname": "${host.hostname}",
                          "port": "${host.port}",
                          "repository_root": "${host.borg_repo_root}",
                          "repository_name": "${container_name}",
                          "ssh_command": "${host.borg_rsh}",
                          "passphrase_variable": "secrets.passphrase",
                          "body": {
                            "environment": { "scope": "parent" },
                            "module": {
                              "cyborg.modules.sequence.v1": {
                                "steps": [
                                  {
                                    "environment": { "scope": "parent" },
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
                                    "environment": { "scope": "parent" },
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
                                    "environment": { "scope": "parent" },
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
        "environment": {
          "scope": "reference",
          "name": "overleaf_job"
        },
        "module": {
          "cyborg.modules.docker.up.v1": {
            "compose_path": "${container_root}/docker-compose.yml",
            "user": "${docker_user}"
          }
        }
      }
    }
  }
}
```

**Environment Flow:**
```
1. "overleaf_job" scope created (inherit_parent from global)
2. Configuration runs → sets secrets.passphrase, container_name, etc.
3. Guard body uses "parent" → same "overleaf_job" scope
4. Docker down reads ${container_root}, ${docker_user} from current scope
5. ForEach creates iteration scopes (inherit from "overleaf_job")
   → Each iteration has ${host} + all parent variables
6. Repository module sets BORG_REPO, BORG_PASSPHRASE in iteration scope
7. Borg create/prune/compact read BORG_* from environment
8. On completion or error: finally references "overleaf_job" scope
   → Reads ${container_root}, ${docker_user} to restart containers
```

### Example: SMB Data Daily Backup (`jobs/daily/smb-data.json`)

This example demonstrates:
- Nested foreach loops (smb_targets → backup_hosts)
- Per-iteration secrets loading via configuration block
- Conditional execution based on directory existence
- Named guard scope for service restart

```json
{
  "environment": {
    "scope": "inherit_parent",
    "name": "smb_job"
  },
  "configuration": {
    "cyborg.modules.config.map.v1": {
      "entries": [
        { "key": "service_name", "string": "smbd.service" }
      ]
    }
  },
  "module": {
    "cyborg.modules.guard.v1": {
      "body": {
        "environment": { "scope": "parent" },
        "module": {
          "cyborg.modules.sequence.v1": {
            "steps": [
              {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.systemd.stop.v1": {
                    "service_name": "${service_name}"
                  }
                }
              },
              {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.foreach.v1": {
                    "collection": "smb_targets",
                    "item_variable": "target",
                    "continue_on_error": false,
                    "body": {
                      "environment": {
                        "scope": "inherit_parent",
                        "name": "smb_target_iteration"
                      },
                      "configuration": {
                        "cyborg.modules.secrets.load.v1": {
                          "source": "${target.secrets_file}",
                          "source_type": "json_file",
                          "environment_prefix": "secrets"
                        }
                      },
                      "module": {
                        "cyborg.modules.if.v1": {
                          "condition": {
                            "variable": "target.root",
                            "operator": "exists"
                          },
                          "then": {
                            "environment": { "scope": "parent" },
                            "module": {
                              "cyborg.modules.foreach.v1": {
                                "collection": "backup_hosts",
                                "item_variable": "host",
                                "body": {
                                  "environment": { "scope": "inherit_parent" },
                                  "module": {
                                    "cyborg.modules.borg.repository.v1": {
                                      "hostname": "${host.hostname}",
                                      "port": "${host.port}",
                                      "repository_root": "${host.borg_repo_root}",
                                      "repository_name": "${target.repository_name}",
                                      "ssh_command": "${host.borg_rsh}",
                                      "passphrase_variable": "secrets.passphrase",
                                      "body": {
                                        "environment": { "scope": "parent" },
                                        "module": {
                                          "cyborg.modules.sequence.v1": {
                                            "steps": [
                                              {
                                                "environment": { "scope": "parent" },
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
                                                "environment": { "scope": "parent" },
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
                                                "environment": { "scope": "parent" },
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
                            "environment": { "scope": "parent" },
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
                  }
                }
              }
            ]
          }
        }
      },
      "finally": {
        "environment": {
          "scope": "reference",
          "name": "smb_job"
        },
        "module": {
          "cyborg.modules.systemd.start.v1": {
            "service_name": "${service_name}"
          }
        }
      }
    }
  }
}
```

**Environment Flow:**
```
1. "smb_job" scope created (inherit_parent from global)
2. Configuration sets service_name = "smbd.service"
3. Guard body uses parent → same "smb_job" scope
4. Systemd stop reads ${service_name}
5. Outer foreach creates "smb_target_iteration" scopes for each target
   → Configuration loads secrets.passphrase per-target
6. If condition checks ${target.root} existence
7. Inner foreach creates host iteration scopes (inherit from target scope)
   → Each has ${host} + ${target} + ${secrets.passphrase}
8. Repository sets BORG_* vars, borg modules execute
9. Finally references "smb_job" to read ${service_name} for restart
```

---

## Global Configuration (`config.json`)

The root configuration establishes the global environment with backup hosts and orchestrates WoL → template execution → cleanup.

```json
{
  "environment": {
    "scope": "global",
    "name": "global"
  },
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
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.foreach.v1": {
              "collection": "backup_hosts",
              "item_variable": "host",
              "body": {
                "environment": { "scope": "inherit_global" },
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
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.template.v1": {
              "templates": [
                { "name": "daily", "path": "jobs/daily.json" },
                { "name": "weekly", "path": "jobs/weekly.json" },
                { "name": "monthly", "path": "jobs/monthly.json" }
              ]
            }
          }
        },
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.foreach.v1": {
              "collection": "backup_hosts",
              "item_variable": "host",
              "body": {
                "environment": { "scope": "inherit_global" },
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

**Environment Flow:**
```
1. Global environment receives backup_hosts collection, allowed_run_as_users
2. WoL module iterates hosts, sets wol_state.{hostname} in global scope
   → Uses inherit_global so state variables persist
3. Template module executes selected job (daily/weekly/monthly)
   → Job inherits global environment (backup_hosts, wol_state.*, etc.)
4. Shutdown module reads wol_state.{hostname} from global
   → Only shuts down hosts that were woken by this run
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