# Module Reference

Complete documentation for all Cyborg modules.

Legend: ✅ Implemented | ⚠️ Definition only (no worker) | ❌ Not yet implemented

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Control Flow Modules](#control-flow-modules)
  - [Sequence Module (`cyborg.modules.sequence.v1`) ✅](#sequence-module-cyborgmodulessequencev1-)
  - [ForEach Module (`cyborg.modules.foreach.v1`) ✅](#foreach-module-cyborgmodulesforeachv1-)
  - [Guard Module (`cyborg.modules.guard.v1`) ✅](#guard-module-cyborgmodulesguardv1-)
  - [If Module (`cyborg.modules.if.v1`) ✅](#if-module-cyborgmodulesifv1-)
    - [IsTrue Condition (`cyborg.modules.if.condition.is_true.v1`) ✅](#istrue-condition-cyborgmodulesifconditionis_truev1-)
    - [IsSet Condition (`cyborg.modules.if.condition.is_set.v1`) ✅](#isset-condition-cyborgmodulesifconditionis_setv1-)
  - [Assert Module (`cyborg.modules.assert.v1`) ✅](#assert-module-cyborgmodulesassertv1-)
  - [Switch Module (`cyborg.modules.switch.v1`) ✅](#switch-module-cyborgmodulesswitchv1-)
  - [Dynamic Module (`cyborg.modules.dynamic.v1`) ✅](#dynamic-module-cyborgmodulesdynamicv1-)
- [External Execution Modules](#external-execution-modules)
  - [Subprocess Module (`cyborg.modules.subprocess.v1`) ✅](#subprocess-module-cyborgmodulessubprocessv1-)
  - [External Module (`cyborg.modules.external.v1`) ✅](#external-module-cyborgmodulesexternalv1-)
  - [Template Module (`cyborg.modules.template.v1`) ✅](#template-module-cyborgmodulestemplatev1-)
- [Configuration Modules](#configuration-modules)
  - [ConfigMap Module (`cyborg.modules.config.map.v1`) ✅](#configmap-module-cyborgmodulesconfigmapv1-)
  - [ConfigCollection Module (`cyborg.modules.config.collection.v1`) ✅](#configcollection-module-cyborgmodulesconfigcollectionv1-)
  - [ExternalConfig Module (`cyborg.modules.config.external.v1`) ✅](#externalconfig-module-cyborgmodulesconfigexternalv1-)
- [Environment Modules](#environment-modules)
  - [Environment Definitions Module (`cyborg.modules.environment.defs.v1`) ✅](#environment-definitions-module-cyborgmodulesenvironmentdefsv1-)
  - [Named Module Reference (`cyborg.modules.named.ref.v1`) ✅](#named-module-reference-cyborgmodulesnamedrefv1-)
- [File System Modules](#file-system-modules)
  - [Glob Module (`cyborg.modules.glob.v1`) ✅](#glob-module-cyborgmodulesglobv1-)
- [Network Modules](#network-modules)
  - [Wake-on-LAN Module (`cyborg.modules.network.wol.v1`) ✅](#wake-on-lan-module-cyborgmodulesnetworkwolv1-)
  - [SSH Shutdown Module (`cyborg.modules.network.ssh_shutdown.v1`) ✅](#ssh-shutdown-module-cyborgmodulesnetworkssh_shutdownv1-)
- [Borg Modules](#borg-modules)
  - [Borg Job Module (`cyborg.modules.borg.job.v1`) ⚠️](#borg-job-module-cyborgmodulesborgjobv1-️)
  - [Borg Backup Module (`cyborg.modules.borg.backup.v1`) ⚠️](#borg-backup-module-cyborgmodulesborgbackupv1-️)

<!-- /code_chunk_output -->


## Control Flow Modules

### Sequence Module (`cyborg.modules.sequence.v1`) ✅

Executes a list of child modules in order.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record SequenceModule(
    [property: MinLength(1)] IReadOnlyCollection<ModuleContext> Steps
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.sequence.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.sequence.v1": {
    "steps": [
      {
        "module": { "cyborg.modules.subprocess.v1": { "command": { "executable": "/usr/bin/ls", "arguments": ["-la"] } } }
      },
      {
        "module": { "cyborg.modules.subprocess.v1": { "command": { "executable": "/usr/bin/echo", "arguments": ["done"] } } }
      }
    ]
  }
}
```

**Worker Behavior:**
1. Iterate through `Steps` in order
2. Execute each step via `runtime.ExecuteAsync(step)`
3. If any step returns `Canceled` or `Failed`, abort immediately with that status
4. If at least one step succeeds, return `Success`; if all steps are skipped, return `Skipped`

---

### ForEach Module (`cyborg.modules.foreach.v1`) ✅

Iterates over a collection in the environment, executing a child module for each item.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record ForeachModule
(
    [property: Required] string Collection,
    [property: Required] string ItemVariable,
    [property: DefaultValue<bool>(false)] bool ContinueOnError,
    [property: Required] ModuleContext Body
) : ModuleBase, IModule
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
    "continue_on_error": false,
    "body": {
      "environment": {
        "scope": "inherit_parent",
        "name": "host_iteration"
      },
      "module": { "cyborg.modules.sequence.v1": { "steps": [] } }
    }
  }
}
```

**Worker Behavior:**
1. Resolve `Collection` variable from environment (must be `IEnumerable<object>`)
2. For each item in the collection:
   - Create a loop environment via `runtime.PrepareEnvironment(Module.Body)`
   - If the item implements `IDecomposable`, publish it with `DecompositionStrategy.FullHierarchy` (e.g., `current_host.hostname`, `current_host.port`)
   - Otherwise, set `ItemVariable` directly
   - Execute `Body` module in the loop environment
3. If a step fails and `ContinueOnError` is `false`, return `Failed` immediately
4. If `ContinueOnError` is `true`, track failure but continue iteration
5. Return `Success` if at least one iteration succeeded, `Skipped` if all skipped

---

### Guard Module (`cyborg.modules.guard.v1`) ✅

Executes a body module with try-catch-finally semantics, guaranteeing cleanup.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record GuardModule
(
    [property: Required] ModuleContext Try,
    ModuleContext? Catch,
    [property: Required] ModuleContext Finally,
    [property: DefinedEnumValue][property: DefaultValue<GuardModuleBehavior>(GuardModuleBehavior.Rethrow)] GuardModuleBehavior Behavior
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.guard.v1";
}

public enum GuardModuleBehavior
{
    Rethrow,
    Swallow,
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.guard.v1": {
    "try": {
      "environment": { "scope": "inherit_parent", "name": "backup_session" },
      "module": {
        "cyborg.modules.sequence.v1": {
          "steps": [
            { "module": { "cyborg.modules.subprocess.v1": { "command": { "executable": "docker", "arguments": ["compose", "down"] } } } }
          ]
        }
      }
    },
    "catch": {
      "environment": { "scope": "reference", "name": "backup_session" },
      "module": { "cyborg.modules.subprocess.v1": { "command": { "executable": "/usr/bin/echo", "arguments": ["error occurred"] } } }
    },
    "finally": {
      "environment": { "scope": "reference", "name": "backup_session" },
      "module": { "cyborg.modules.subprocess.v1": { "command": { "executable": "docker", "arguments": ["compose", "up", "-d"] } } }
    },
    "behavior": "rethrow"
  }
}
```

**Worker Behavior:**
1. Execute `Try` block, capture result
2. If `Try` fails or throws a non-cancellation exception:
   - If `Catch` is defined, execute it and use its exit status
   - If no `Catch` and `Behavior` is `Swallow`, return `Success`
   - If no `Catch` and `Behavior` is `Rethrow`, return `Failed`
3. Prevents double-handling: if the catch block itself fails after a `Try` failure, returns `Failed` immediately
4. `Finally` block always executes (unless cancelled), regardless of `Try`/`Catch` outcome
5. Returns the final resolved exit status

**Key Scoping Pattern:**
- `Try` creates a named environment (e.g., `"backup_session"`) with `inherit_parent` scope
- `Catch` and `Finally` use `reference` scope to access the same environment
- This allows `Finally` to read variables set during `Try` execution

---

### If Module (`cyborg.modules.if.v1`) ✅

Conditionally executes modules based on a condition module's result.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record IfModule
(
    [property: Required] ModuleReference Condition,
    [property: Required] ModuleContext Then,
    ModuleContext? Else = null
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.if.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.if.v1": {
    "condition": {
      "cyborg.modules.if.condition.is_set.v1": {
        "variable": "smb_root"
      }
    },
    "then": {
      "environment": { "scope": "parent" },
      "module": { "cyborg.modules.subprocess.v1": { "command": { "executable": "/usr/bin/echo", "arguments": ["found"] } } }
    },
    "else": {
      "environment": { "scope": "parent" },
      "module": { "cyborg.modules.subprocess.v1": { "command": { "executable": "/usr/bin/echo", "arguments": ["not found"] } } }
    }
  }
}
```

**Worker Behavior:**
1. Create an isolated environment for the condition module
2. Override the condition's artifact output to write to a known location in the parent environment
3. Execute the `Condition` module, which must return a `ConditionalResult` with a boolean `Result`
4. If `Condition` does not succeed, propagate its status
5. If `Result` is `true`, execute the `Then` branch
6. If `Result` is `false`, execute the `Else` branch (or skip if `Else` is not defined)
7. Return the executed branch's status (or `Skipped`)

**Condition Modules:** The `Condition` must be a module that produces a `ConditionalResult(bool Result)`. Built-in condition modules include `IsTrue` and `IsSet` (see below).

---

#### IsTrue Condition (`cyborg.modules.if.condition.is_true.v1`) ✅

Checks whether an environment variable resolves to a boolean `true` value.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record IsTrueModule
(
    [property: Required] string Variable
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.if.condition.is_true.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.if.condition.is_true.v1": {
    "variable": "feature_enabled"
  }
}
```

**Worker Behavior:**
1. Resolve `Variable` from the environment as a `bool`
2. If the variable is undefined or cannot be resolved as a bool, return `Failed`
3. Return `Success` with `ConditionalResult(value)`

---

#### IsSet Condition (`cyborg.modules.if.condition.is_set.v1`) ✅

Checks whether an environment variable is defined (regardless of value).

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record IsSetModule
(
    [property: Required] string Variable
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.if.condition.is_set.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.if.condition.is_set.v1": {
    "variable": "docker_user"
  }
}
```

**Worker Behavior:**
1. Check whether `Variable` is defined in the environment
2. Return `Success` with `ConditionalResult(true)` if set, `ConditionalResult(false)` if not

---

### Assert Module (`cyborg.modules.assert.v1`) ✅

Validates a condition and fails with a message if the assertion is false.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record AssertModule
(
    [property: Required] ModuleReference Assertion,
    [property: Required] string Message
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.assert.v1";
}
```

**Result Type:**
```csharp
[GeneratedDecomposition]
public sealed partial record AssertModuleResult(string Message) : IDecomposable;
```

**JSON Configuration:**
```json
{
  "cyborg.modules.assert.v1": {
    "assertion": {
      "cyborg.modules.if.condition.is_set.v1": {
        "variable": "backup_target"
      }
    },
    "message": "Required variable 'backup_target' is not defined"
  }
}
```

**Worker Behavior:**
1. Execute `Assertion` module (must return a `ConditionalResult`)
2. If the assertion module itself fails, propagate that status
3. If `ConditionalResult.Result` is `false`, return `Failed` with `AssertModuleResult(Message)` (message is interpolated)
4. If `ConditionalResult.Result` is `true`, return `Success`

---

### Switch Module (`cyborg.modules.switch.v1`) ✅

Dispatches execution to one of several named cases based on an environment variable's value.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record SwitchModule
(
    [property: Required] string Variable,
    [property: MinLength(1)] ImmutableArray<SwitchReference> Cases
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.switch.v1";
}

[Validatable]
public sealed record SwitchReference
(
    [property: Required] string Name,
    [property: Required] string Path
);
```

**JSON Configuration:**
```json
{
  "cyborg.modules.switch.v1": {
    "variable": "backup_strategy",
    "cases": [
      { "name": "full", "path": "/etc/cyborg/strategies/full.json" },
      { "name": "incremental", "path": "/etc/cyborg/strategies/incremental.json" }
    ]
  }
}
```

**Worker Behavior:**
1. Resolve `Variable` from the global runtime environment as a string
2. Look up the matching `Name` in `Cases`
3. Load the module configuration from the matched case's `Path`
4. Execute the loaded module
5. Throws if variable cannot be resolved or no matching case is found

---

### Dynamic Module (`cyborg.modules.dynamic.v1`) ✅

A passthrough wrapper that executes a child module in its body context. Allows dynamic resolution of the child module at runtime via environment overrides.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record DynamicModule
(
    [property: Required] ModuleContext Body
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.dynamic.v1";
}
```

**JSON Configuration:**
```json
{
  "configuration": {
    "cyborg.modules.config.map.v1": {
      "entries": [
        {
          "key": "@my_dynamic.body",
          "cyborg.types.module.context.v1": {
            "module": { 
              "cyborg.modules.subprocess.v1": { 
                "command": { "executable": "/usr/bin/echo", "arguments": ["hello"] } 
              } 
            }
          }
        }
      ]
    }
  },
  "cyborg.modules.dynamic.v1": {
    "name": "my_dynamic"
  }
}
```

**Worker Behavior:**
1. Automatically resolves overrides for the `Body` property from the environment (e.g., `@my_dynamic.body`) as usual
2. Executes the resolved `Body` module
3. Returns the executed body's status

---

## External Execution Modules

### Subprocess Module (`cyborg.modules.subprocess.v1`) ✅

Executes an external process with optional impersonation and output capture.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record SubprocessModule
(
    [property: Required] SubprocessCommand Command,
    [property: Required][property: DefaultInstance] SubprocessOutputOptions Output,
    [property: DefaultValue<bool>(true)] bool CheckExitCode,
    ImpersonationContext? Impersonation
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.subprocess.v1";
}

[Validatable]
public sealed record ImpersonationContext
(
    [property: Required][property: DefaultValue<string>("/usr/bin/runuser")][property: FileExists] string Executable,
    [property: Required] string User
);

[Validatable]
public sealed record SubprocessCommand
(
    [property: Required][property: FileExists] string Executable,
    [property: Required] ImmutableArray<string> Arguments
);

[Validatable]
public sealed record SubprocessOutputOptions
(
    bool ReadStdout,
    bool ReadStderr
) : IDefaultInstance<SubprocessOutputOptions>
{
    public static SubprocessOutputOptions Default => new(ReadStdout: false, ReadStderr: false);
}
```

**Result Type:**
```csharp
[GeneratedDecomposition]
public sealed partial record SubprocessModuleResult(int ExitCode, string? Stdout, string? Stderr) : IDecomposable;
```

**JSON Configuration:**
```json
{
  "cyborg.modules.subprocess.v1": {
    "command": {
      "executable": "/usr/bin/borg",
      "arguments": ["create", "--stats", "::archive-{now}"]
    },
    "output": {
      "read_stdout": true,
      "read_stderr": true
    },
    "check_exit_code": true,
    "impersonation": {
      "executable": "/usr/bin/runuser",
      "user": "docker"
    }
  }
}
```

**Worker Behavior:**
1. Build process arguments from `Command`
2. If `Impersonation` is set, wrap with `runuser -u <user> -- <executable> <args>`
3. Configure stdout/stderr redirection based on `Output` options
4. Execute via `IChildProcessDispatcher`
5. If `CheckExitCode` is `true` and exit code ≠ 0, return `Failed` with result
6. Otherwise return `Success` with `SubprocessModuleResult`

---

### External Module (`cyborg.modules.external.v1`) ✅

Loads and executes a module from an external JSON configuration file.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record ExternalModule
(
    [property: Required][property: FileExists] string Path
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.external.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.external.v1": {
    "path": "/etc/cyborg/jobs/daily-backup.json"
  }
}
```

**Worker Behavior:**
1. Load module configuration from `Path` via `IModuleConfigurationLoader`
2. Execute the loaded module
3. Return the loaded module's execution status

---

### Template Module (`cyborg.modules.template.v1`) ✅

Loads and executes an external module with namespaced arguments injected into the environment.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record TemplateModule
(
    [property: Required][property: MustMatch(nameof(TemplateModule.NamespaceRegex))] string Namespace,
    [property: Required][property: FileExists] string Path,
    ImmutableArray<DynamicKeyValuePair> Arguments
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.template.v1";

    [GeneratedRegex(@"^[A-Za-z0-9_](\.[A-Za-z0-9_\-]+)*$")]
    private static partial Regex NamespaceRegex { get; }
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.template.v1": {
    "namespace": "backup.overleaf",
    "path": "/etc/cyborg/templates/docker-backup.json",
    "arguments": [
      { "key": "container_name", "string": "overleaf" },
      { "key": "compose_path", "string": "/opt/docker/containers/overleaf/docker-compose.yml" }
    ]
  }
}
```

**Worker Behavior:**
1. For each argument in `Arguments`, set the variable under the namespace path (e.g., `backup.overleaf.container_name`)
2. Load module configuration from `Path`
3. Execute the loaded module
4. Return the loaded module's execution status

---

## Configuration Modules

Configuration modules implement `IConfigurationModule` and are used in the `configuration` property of a `ModuleContext` to set up environment variables before the main module executes.

### ConfigMap Module (`cyborg.modules.config.map.v1`) ✅

Sets key-value pairs in the current environment.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record ConfigMapModule(
    [property: MinLength(1)] ImmutableArray<DynamicKeyValuePair> Entries
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.config.map.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.config.map.v1": {
    "entries": [
      { "key": "container_name", "string": "overleaf" },
      { "key": "compose_path", "string": "/opt/docker/containers/overleaf/docker-compose.yml" },
      { "key": "ssh_port", "int": 22 }
    ]
  }
}
```

**Worker Behavior:**
1. Runtime resolves stronly-typed values from the JSON configuration based on registered type providers (e.g., `string`, `int`, `bool`, `collection<T>`, custom types, etc.) 
2. For each `DynamicKeyValuePair` in `Entries`, set the key-value pair in the current runtime environment
3. Return `Success`

---

### ConfigCollection Module (`cyborg.modules.config.collection.v1`) ✅

Aggregates multiple configuration sources into a single configuration block.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record ConfigCollectionModule
(
    [property: MinLength(1)] ImmutableArray<ModuleReference> Sources
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.config.collection.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.config.collection.v1": {
    "sources": [
      {
        "cyborg.modules.config.map.v1": {
          "entries": [
            { "key": "container_name", "string": "overleaf" }
          ]
        }
      },
      {
        "cyborg.modules.config.external.v1": {
          "path": "/etc/cyborg/config/shared.json"
        }
      }
    ]
  }
}
```

**Worker Behavior:**
1. Validate all `Sources` implement `IConfigurationModule`
2. Execute each source in order in the current runtime environment
3. If any source returns `Canceled` or `Failed`, abort with that status
4. Return `Success` if at least one source succeeds, `Skipped` if all skipped

---

### ExternalConfig Module (`cyborg.modules.config.external.v1`) ✅

Loads and executes configuration from an external file.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record ExternalConfigModule
(
    [property: Required][property: FileExists] string Path
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.config.external.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.config.external.v1": {
    "path": "/etc/cyborg/config/hosts.json"
  }
}
```

**Worker Behavior:**
1. Load module configuration from `Path` via `IModuleConfigurationLoader`
2. Execute the loaded module's configuration in the current runtime environment
3. Return execution status

---

## Environment Modules

### Environment Definitions Module (`cyborg.modules.environment.defs.v1`) ✅

Pre-creates named environment scopes for later reference by other modules.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record EnvironmentDefinitionsModule
(
    [property: Required][property: MinLength(1)] ImmutableArray<ModuleEnvironment> Environments
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.environment.defs.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.environment.defs.v1": {
    "environments": [
      { "scope": "inherit_parent", "name": "backup_session" },
      { "scope": "isolated", "name": "temp_workspace" }
    ]
  }
}
```

**Worker Behavior:**
1. For each `ModuleEnvironment` in `Environments`, call `runtime.PrepareEnvironment(environment)` to create the scope
2. Return `Success`

---

### Named Module Reference (`cyborg.modules.named.ref.v1`) ✅

Executes a module registered in the `IModuleRegistry` by name. If modules specify a `Name` property, they are automatically registered in the registry during JSON deserialization and can be referenced by this module.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record NamedModuleReferenceModule(
    [property: Required] string Target
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.named.ref.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.named.ref.v1": {
    "target": "daily_backup"
  }
}
```

**Worker Behavior:**
1. Look up the module by `Target` name in `IModuleRegistry`
2. Execute the resolved module
3. Throws if no module with the given name is found

---

## File System Modules

### Glob Module (`cyborg.modules.glob.v1`) ✅

Matches files in a directory using a regex pattern.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record GlobModule
(
    [property: Required] string Pattern,
    [property: Required][property: DirectoryExists] string Root,
    [property: DefaultValue<bool>(false)] bool Recurse
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.glob.v1";
}
```

**Result Type:**
```csharp
[GeneratedDecomposition]
public sealed partial record GlobModuleResult(IEnumerable<string> Files) : IDecomposable;
```

**JSON Configuration:**
```json
{
  "cyborg.modules.glob.v1": {
    "pattern": "\\.conf$",
    "root": "/etc/cyborg/jobs",
    "recurse": true
  }
}
```

**Worker Behavior:**
1. Compile `Pattern` as a case-insensitive `Regex`
2. Enumerate files from `Root` (recursively if `Recurse` is `true`)
3. Filter files matching the regex
4. Return `Success` with `GlobModuleResult` containing matching file paths

---

## Network Modules

### Wake-on-LAN Module (`cyborg.modules.network.wol.v1`) ✅

Wakes a remote host via Wake-on-LAN and waits for port availability.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record WakeOnLanModule
(
    [property: Required] string TargetHost,
    [property: Required][property: MustMatch(nameof(WakeOnLanModule.MacAddressRegex))] string MacAddress,
    [property: Required][property: Range<int>(Min = 1, Max = ushort.MaxValue)] int LivenessProbePort,
    [property: DefaultTimeSpan("00:05:00")] TimeSpan MaxWaitTime,
    [property: DefaultTimeSpan("00:00:30")] TimeSpan HostDiscoveryTimeout,
    [property: Required][property: DefaultValue<string>("/usr/bin/wakeonlan")][property: FileExists] string Executable
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.wol.v1";

    [GeneratedRegex(@"^([0-9A-Fa-f]{2}[:\-]){5}([0-9A-Fa-f]{2})$")]
    private static partial Regex MacAddressRegex { get; }
}
```

**Result Type:**
```csharp
[GeneratedDecomposition]
public sealed partial record WakeOnLanModuleResult(bool WokeUp) : IDecomposable;
```

**JSON Configuration:**
```json
{
  "cyborg.modules.network.wol.v1": {
    "target_host": "backup1.service.local",
    "mac_address": "11:22:33:44:55:66",
    "liveness_probe_port": 22,
    "max_wait_time": "00:05:00",
    "host_discovery_timeout": "00:00:30"
  }
}
```

**Worker Behavior:**
1. Ping `TargetHost` with `HostDiscoveryTimeout`
2. If already reachable, return `Success` with `WokeUp: false`
3. If unreachable, execute `wakeonlan -i <host> <mac>` via subprocess
4. If wakeonlan fails (non-zero exit code), throw
5. Probe `LivenessProbePort` with `MaxWaitTime` timeout while the target host is booting up
6. If port responds, return `Success` with `WokeUp: true`
7. If timeout expires, return `Failed` with `WokeUp: false`

---

### SSH Shutdown Module (`cyborg.modules.network.ssh_shutdown.v1`) ✅

Shuts down a remote host via SSH.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record SshShutdownModule
(
    [property: Required][property: DefaultValue<string>("/usr/bin/ssh")][property: FileExists] string Executable,
    [property: Required] string Hostname,
    [property: Required] string Username,
    [property: Required][property: Range<int>(Min = 1, Max = ushort.MaxValue)][property: DefaultValue<int>(22)] int Port,
    [property: Required][property: DefaultValue<string>("/usr/bin/shutdown -h now")] string ShutdownCommand,
    SshPass? SshPass
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.ssh_shutdown.v1";
}

[Validatable]
public sealed record SshPass
(
    [property: Required][property: DefaultValue<string>("/usr/bin/sshpass")][property: FileExists] string Executable,
    [property: Required] string FilePath,
    string? MatchPrompt
);
```

**Result Type:**
```csharp
[GeneratedDecomposition]
public sealed partial record SshShutdownModuleResult(int ExitCode, string? StandardOutput, string? StandardError) : IDecomposable;
```

**JSON Configuration:**
```json
{
  "cyborg.modules.network.ssh_shutdown.v1": {
    "hostname": "backup1.service.local",
    "username": "root",
    "port": 22,
    "shutdown_command": "/usr/bin/shutdown -h now",
    "ssh_pass": {
      "file_path": "/root/.ssh/pass",
      "match_prompt": "assphrase"
    }
  }
}
```

**Worker Behavior:**
1. Build SSH arguments: `<username>@<hostname>:<port> <shutdown_command>`
2. If `SshPass` is configured, wrap with `sshpass -f<file_path> [-P <match_prompt>] ssh <args>`
3. Execute via `IChildProcessDispatcher`
4. Return `Failed` if exit code ≠ 0, `Success` otherwise

---

## Borg Modules

> **Note:** Borg modules have record definitions but no worker implementations yet. They live in the separate `Cyborg.Modules.Borg` project.

### Borg Job Module (`cyborg.modules.borg.job.v1`) ⚠️

> **Status:** Definition only — no worker implementation

Orchestrates a borg backup job with lifecycle hooks.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record BorgJobModule
(
    ModuleContext Job,
    ModuleContext? BeforeJob,
    ModuleContext? AfterJob,
    ModuleContext? OnError
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.borg.job.v1";
}
```

---

### Borg Backup Module (`cyborg.modules.borg.backup.v1`) ⚠️

> **Status:** Definition only — no worker implementation

Defines borg backup remote targets.

**Module Record:**
```csharp
public sealed record BorgBackupModule(
    ImmutableArray<BorgRemote> Remotes
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.borg.backup.v1";
}

[GeneratedDecomposition]
public sealed partial record BorgRemote(
    string Hostname,
    int Port,
    string? WakeOnLanMac,
    string BorgRsh,
    string BorgRepoRoot
);
```

`BorgRemote` implements `IDecomposable` and has a `BorgRemoteValueProvider : IDynamicValueProvider` for JSON deserialization via the `cyborg.types.borg.remote.v1` type name.
