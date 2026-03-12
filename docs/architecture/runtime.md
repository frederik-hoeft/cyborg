# Runtime Infrastructure

The POC already provides a sophisticated environment and scoping system that the migration design must leverage, not duplicate.

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

## Environment Scoping (`Cyborg.Core.Modules.Runtime`)

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

## Variable Resolution

`RuntimeEnvironment.TryResolveVariable<T>()` supports:
1. **Direct lookup** - Variable name maps directly to stored value
2. **Single indirection** - String values matching `${variable_name}` resolve recursively to the referenced variable
3. **String interpolation** - String values containing multiple `${var}` placeholders are interpolated with resolved values
4. **Type casting** - Generic `T` constraint ensures type-safe retrieval

**Variable Name Pattern:** `[A-Za-z_][A-Za-z_0-9\-\.]*` (letters, underscores, digits, hyphens, dots)

```csharp
// Single indirection: "${job_name}" resolves to the value of "job_name"
// String interpolation: "backup-${host}-${date}" becomes "backup-server1-2026-03-12"
protected virtual string InterpolateString(string stringValue)
{
    // Uses regex to find all ${variable_name} patterns and replaces them
    // Unresolvable variables are left as-is in the output
}
```

## Module Property Overrides

The `Resolve<TModule, T>()` method enables runtime overrides of module properties via specially-prefixed environment variables. This is the mechanism used by the source-generated `ResolveOverridesAsync()` implementation.

**Override Resolution Order:**
1. `@{module.Name}.{property_path}` - Override by module instance name (if module has a `name` property)
2. `@{ModuleId}.{property_path}` - Override by module ID

**Use Case:** Injecting non-string values into modules when the source is a string variable.

```json
// Problem: host.port is an integer, but ${host.port} only works for strings
// Solution: Use an override with the @ prefix

{
  "configuration": {
    "cyborg.modules.config.map.v1": {
      "entries": [
        // Override my_wol.liveness_probe_port with the resolved value of ${host.port}
        { "key": "@my_wol.liveness_probe_port", "string": "${host.port}" }
      ]
    }
  },
  "module": {
    "cyborg.modules.network.wol.v1": {
      "name": "my_wol",
      "target_host": "${host.hostname}",
      "mac_address": "${host.wake_on_lan_mac}",
      "liveness_probe_port": 22  // Default value, overridden by @my_wol.liveness_probe_port
    }
  }
}
```

**How it works:**
1. Module defines `name: "my_wol"` to enable named overrides
2. Configuration sets `@my_wol.liveness_probe_port` = `"${host.port}"` in environment
3. During `ResolveOverridesAsync()`, the generated code calls `runtime.Environment.Resolve(module, module.LivenessProbePort, ...)`
4. `Resolve()` looks up `@my_wol.liveness_probe_port`, finds the string `"${host.port}"`
5. String is interpolated to the actual port value (e.g., `"22"`)
6. Value is type-converted to `int` and replaces the module property

**Property path conversion:** Property names are converted to snake_case via `JsonNamingPolicy` (e.g., `LivenessProbePort` → `liveness_probe_port`).

## Named Environment Persistence

Non-transient environments (those with explicit `Name` in JSON config) are registered with the root runtime via `TryAddEnvironment()` and can be retrieved later via `TryGetEnvironment()`. This enables:
- **Cross-step state sharing** - Step 1 creates named environment, Step 3 references it
- **Cleanup scopes** - Finally block references the same environment as try block

## Configuration Loading (`ModuleContext`)

Each module invocation is wrapped in `ModuleContext`:
```csharp
record ModuleContext(
    ModuleReference Module,           // The actual module to execute
    ModuleEnvironment? Environment,   // Scope configuration
    ModuleReference? Configuration    // Optional: ConfigMap/ConfigCollection to populate environment
);
```

The `Configuration` module executes first, populating the environment, before the main `Module` executes.

## Dynamic Value Providers

The `IDynamicValueProvider` system enables type-safe JSON deserialization for config values:
- Built-in types: `string`, `bool`, `int`, `long`, `double`, `decimal`, etc.
- Typed collections: `collection<T>` where `T` is a registered type
- Extensible via custom `IDynamicValueProvider` implementations

**JSON syntax for scalars:**
```json
{ "key": "max_retries", "int": 3 }
{ "key": "enable_compression", "bool": true }
{ "key": "container_name", "string": "overleaf" }
```

**JSON syntax for typed collections:**
```json
{
  "key": "backup_hosts",
  "collection<cyborg.types.borg.remote.v1>": [
    {
      "hostname": "backup1.service.local",
      "port": 22,
      "wake_on_lan_mac": "aa:bb:cc:dd:ee:ff",
      "borg_rsh": "/usr/bin/sshpass -f/root/.ssh/pass -P assphrase /usr/bin/ssh",
      "borg_repo_root": "/var/backups/borg/my-client.service.local"
    }
  ]
}
```

**Custom type registration:**
```csharp
// Register a custom dynamic value provider (e.g., for BorgRemote)
[GeneratedDecomposition]
public sealed partial record BorgRemote(string Hostname, int Port, string? WakeOnLanMac, string BorgRsh, string BorgRepoRoot);

public sealed class BorgRemoteValueProvider : IDynamicValueProvider
{
    public string TypeName => "cyborg.types.borg.remote.v1";
    
    public bool TryCreateValue(ref Utf8JsonReader reader, IModuleLoaderContext context, out DynamicValue? value)
    {
        BorgRemote? remote = JsonSerializer.Deserialize<BorgRemote>(ref reader, context);
        value = remote is not null ? new DynamicValue(remote) : null;
        return remote is not null;
    }
}
```

The `[GeneratedDecomposition]` attribute generates `IDecomposable` implementation, enabling dot-notation access to properties when the object is set as an environment variable (e.g., `${host.hostname}`, `${host.port}`).

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
