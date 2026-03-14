# Module Reference

Complete documentation for all Cyborg modules.

Legend: ✅ Implemented | 🚧 Partial/WIP | ❌ Planned

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Core Control Flow Modules](#core-control-flow-modules)
  - [ForEach Module (`cyborg.modules.foreach.v1`) ✅](#foreach-module-cyborgmodulesforeachv1-)
  - [Guard Module (`cyborg.modules.guard.v1`) 🚧](#guard-module-cyborgmodulesguardv1-)
  - [Conditional Module (`cyborg.modules.if.v1`) ✅](#conditional-module-cyborgmodulesifv1-)
- [Borg Modules](#borg-modules)
  - [Repository Configuration Module (`cyborg.modules.borg.repository.v1`) 🚧](#repository-configuration-module-cyborgmodulesborgrepositoryv1-)
  - [Borg Create Module (`cyborg.modules.borg.create.v1`) 🚧](#borg-create-module-cyborgmodulesborgcreatev1-)
  - [Borg Prune Module (`cyborg.modules.borg.prune.v1`) 🚧](#borg-prune-module-cyborgmodulesborgprunev1-)
  - [Borg Compact Module (`cyborg.modules.borg.compact.v1`) 🚧](#borg-compact-module-cyborgmodulesborgcompactv1-)
- [Service Management Modules](#service-management-modules)
  - [Docker Compose Down Module (`cyborg.modules.docker.down.v1`) 🚧](#docker-compose-down-module-cyborgmodulesdockerdownv1-)
  - [Docker Compose Up Module (`cyborg.modules.docker.up.v1`) 🚧](#docker-compose-up-module-cyborgmodulesdockerupv1-)
  - [Systemd Start Module (`cyborg.modules.systemd.start.v1`) 🚧](#systemd-start-module-cyborgmodulessystemdstartv1-)
  - [Systemd Stop Module (`cyborg.modules.systemd.stop.v1`) 🚧](#systemd-stop-module-cyborgmodulessystemdstopv1-)
- [Network Modules](#network-modules)
  - [Wake-on-LAN Module (`cyborg.modules.network.wol.v1`) ✅](#wake-on-lan-module-cyborgmodulesnetworkwolv1-)
  - [SSH Shutdown Module (`cyborg.modules.network.ssh_shutdown.v1`) 🚧](#ssh-shutdown-module-cyborgmodulesnetworkssh_shutdownv1-)
  - [Network Ping Module (`cyborg.modules.net.ping.v1`) 🚧](#network-ping-module-cyborgmodulesnetpingv1-)
- [Security Modules](#security-modules)
  - [Secrets Load Module (`cyborg.modules.secrets.load.v1`) 🚧](#secrets-load-module-cyborgmodulessecretsloadv1-)
- [System Modules](#system-modules)
  - [Run-As Module (`cyborg.modules.system.run_as.v1`) 🚧](#run-as-module-cyborgmodulessystemrun_asv1-)
- [Logging Modules](#logging-modules)
  - [Log Module (`cyborg.modules.log.v1`) 🚧](#log-module-cyborgmoduleslogv1-)

<!-- /code_chunk_output -->


## Core Control Flow Modules

### ForEach Module (`cyborg.modules.foreach.v1`) ✅

Iterates over a collection in the environment, executing a child module for each item.

**Status:** Implemented

**Purpose:** Replace bash `for` loops over backup hosts, SMB targets, etc.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record ForeachModule
(
    [property: Required][property: MinLength(1)] string Collection,
    [property: Required][property: MinLength(1)] string ItemVariable,
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
      "module": { "cyborg.modules.borg.create.v1": { ... } }
    }
  }
}
```

**Worker Behavior:**
1. Resolve collection variable from environment (fail if not found)
2. Prepare loop environment using `runtime.PrepareEnvironment(Module.Body)`
3. For each item in collection:
   - Set `item_variable` in environment
   - Recursively decompose `IDecomposable` items to dot-notation vars (e.g., `host.hostname`)
   - Execute body module
   - If failure and `continue_on_error` is false, abort
4. Return aggregate result

**Key Features:**
- Uses `runtime.PrepareEnvironment()` to create iteration scope based on `body.environment`
- Recursively decomposes `IDecomposable` items (e.g., `BorgRemote`) into dot-notation variables (`host.hostname`, `host.port`, etc.)
- Named environments (`body.environment.name`) are registered for later `Reference` scope usage

---

### Guard Module (`cyborg.modules.guard.v1`) 🚧

> **Status:** Planned - not yet implemented

Executes a body module with guaranteed cleanup, similar to try-finally semantics.

**Purpose:** Ensure services are restarted even if backup fails (replaces bash `restore_current` pattern).

**Planned Module Record:**
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
1. Execute `body` module, capture result
2. If body failed and `on_error` is defined, execute it (can reference body's named environment)
3. In `finally` block (always runs, even on cancellation):
   - Execute `finally` module with `CancellationToken.None` to ensure cleanup
   - Runtime automatically cleans up transient environments
4. Return body result

---

### Conditional Module (`cyborg.modules.if.v1`) ✅

Conditionally executes modules based on another module's success/failure result.

**Status:** Implemented

**Purpose:** Handle conditional execution where the condition itself is a module (e.g., check if a file exists via subprocess, or any module that returns success/failure).

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
      "cyborg.modules.subprocess.v1": {
        "executable": "/usr/bin/test",
        "arguments": ["-d", "${smb_root}"]
      }
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
1. Execute condition module in current environment
2. If condition succeeded, execute `then` branch; otherwise execute `else` branch (or `then` if no `else`)
3. Return branch execution result

**Key Features:**
- Condition is any module that returns success (true) or failure (false)
- Enables flexible conditions: subprocess exit codes, file existence checks, or custom condition modules
- If no `else` branch and condition fails, executes `then` branch (fallback behavior)

---

## Borg Modules

> **Note:** Borg modules are currently in design phase. A `BorgJobModule` wrapper exists but specific borg command modules are not yet implemented.

### Repository Configuration Module (`cyborg.modules.borg.repository.v1`) 🚧

> **Status:** Planned - not yet implemented

Sets up borg repository environment variables for child modules. Uses standard environment scoping.

**Purpose:** Centralize repository configuration, inject `BORG_REPO` and `BORG_RSH` environment variables for child modules.

**Planned Module Record:**
```csharp
public sealed record BorgRepositoryModule(
    string Hostname,                    // Remote host (e.g., "backup1.service.local")
    int Port,                           // SSH port (e.g., 22)
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
1. Resolve hostname, port, repository root/name from module properties (may contain `${var}` indirection)
2. Construct `BORG_REPO` URL: `ssh://borg@{hostname}:{port}{repoRoot}/{repoName}`
3. Set `BORG_REPO` in current environment scope
4. If `ssh_command` specified, set `BORG_RSH`
5. If `passphrase_variable` specified, resolve passphrase from environment and set `BORG_PASSPHRASE`
6. Execute body module (inherits borg vars via scoping)
7. In `finally`: clear `BORG_PASSPHRASE` for security

**Environment Flow:**
1. Parent scope sets secrets via `ConfigMapModule` (e.g., `borg_passphrase`)
2. `BorgRepositoryModule` reads passphrase from `passphrase_variable`
3. Module sets `BORG_REPO`, `BORG_RSH`, `BORG_PASSPHRASE` in current environment
4. Body's `scope: parent` shares the same environment
5. Child borg modules (`create`, `prune`, `compact`) read from environment
6. Cleanup removes `BORG_PASSPHRASE`

---

### Borg Create Module (`cyborg.modules.borg.create.v1`) 🚧

> **Status:** Planned - not yet implemented

Executes `borg create` to create a backup archive.

**Purpose:** Type-safe borg create invocation with all supported options.

**Planned Module Record:**
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
2. Build argument list programmatically: `create`, flags (`--show-rc`, `--stats`), compression, excludes, archive pattern, paths
3. Execute subprocess with environment variables (array-based args, no shell expansion = injection-safe)
4. Parse stdout via grammar-based parser
5. Record Prometheus metrics

---

### Borg Prune Module (`cyborg.modules.borg.prune.v1`) 🚧

> **Status:** Planned - not yet implemented

Executes `borg prune` to remove old archives.

**Planned Module Record:**
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

### Borg Compact Module (`cyborg.modules.borg.compact.v1`) 🚧

> **Status:** Planned - not yet implemented

Executes `borg compact` to reclaim disk space.

**Planned Module Record:**
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

> **Status:** All service management modules are planned but not yet implemented.

### Docker Compose Down Module (`cyborg.modules.docker.down.v1`) 🚧

> **Status:** Planned - not yet implemented

Stops a Docker Compose stack.

**Purpose:** Replace `docker_down()` bash function with type-safe subprocess invocation.

**Planned Module Record:**
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

### Docker Compose Up Module (`cyborg.modules.docker.up.v1`) 🚧

> **Status:** Planned - not yet implemented

Starts a Docker Compose stack.

**Planned Module Record:**
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

### Systemd Start Module (`cyborg.modules.systemd.start.v1`) 🚧

> **Status:** Planned - not yet implemented

Starts a systemd service.

**Planned Module Record:**
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

### Systemd Stop Module (`cyborg.modules.systemd.stop.v1`) 🚧

> **Status:** Planned - not yet implemented

Stops a systemd service.

**Planned Module Record:**
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

### Wake-on-LAN Module (`cyborg.modules.network.wol.v1`) ✅

Wakes a remote host via Wake-on-LAN and waits for port availability.

**Status:** Implemented

**Purpose:** Replace `borg_poke_backup_host()` function.

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record WakeOnLanModule
(
    [property: Required] string TargetHost,
    [property: Required][property: ExactLength(17)] string MacAddress,
    [property: Required][property: Range<int>(Min = 1, Max = ushort.MaxValue)] int LivenessProbePort,
    [property: Required] string StateVariable,
    [property: DefaultTimeSpan("00:05:00")] TimeSpan MaxWaitTime,
    [property: DefaultTimeSpan("00:00:30")] TimeSpan HostDiscoveryTimeout,
    [property: Required][property: DefaultValue<string>("/usr/bin/wakeonlan")] string Executable
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.wol.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.network.wol.v1": {
    "target_host": "backup1.service.local",
    "mac_address": "11:22:33:44:55:66",
    "liveness_probe_port": 22,
    "state_variable": "wol_state.backup1",
    "max_wait_time": "00:05:00",
    "host_discovery_timeout": "00:00:30"
  }
}
```

**Worker Behavior:**
1. Ping host to check current state
2. If unreachable:
   a. Resolve hostname to IP
   b. Send WoL magic packet via `wakeonlan` subprocess
   c. Poll liveness probe port until reachable or timeout
   d. Set `state_variable = "woken"` in environment
3. If already reachable:
   a. Set `state_variable = "was_up"` in environment
4. Return success/failure

---

### SSH Shutdown Module (`cyborg.modules.network.ssh_shutdown.v1`) 🚧

> **Status:** Partially implemented - module record exists, worker not yet implemented

Shuts down a remote host via SSH (for cleanup after WoL).

**Module Record:**
```csharp
[GeneratedModuleValidation]
public sealed partial record SshShutdownModule
(
    [property: Required] string Hostname,
    [property: Required][property: Range<int>(Min = 1, Max = ushort.MaxValue)] int Port
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.ssh_shutdown.v1";
}
```

**JSON Configuration:**
```json
{
  "cyborg.modules.ssh.shutdown.v1": {
    "hostname": "backup1.service.local",
    "port": 22,
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

### Network Ping Module (`cyborg.modules.net.ping.v1`) 🚧

> **Status:** Planned - not yet implemented

Checks if a host is reachable via ICMP ping.

**Planned Module Record:**
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
    "hostname": "backup1.service.local",
    "timeout_seconds": 10,
    "result_variable": "host_reachable"
  }
}
```

---

## Security Modules

> **Status:** Security modules are planned but not yet implemented.

### Secrets Load Module (`cyborg.modules.secrets.load.v1`) 🚧

> **Status:** Planned - not yet implemented

Loads secrets from a secure source into the runtime environment.

**Purpose:** Replace bash `.secrets` file sourcing with secure, structured secret loading.

**Planned Module Record:**
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
1. Load secrets from source using specified `source_type`
2. Filter to requested keys (if specified)
3. Set each secret as `{prefix}.{key}` in current environment scope
4. Return success

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

> **Status:** System modules are planned but not yet implemented.

### Run-As Module (`cyborg.modules.system.run_as.v1`) 🚧

> **Status:** Planned - not yet implemented

Executes child modules as a different user.

**Purpose:** Replace `su -c` patterns for running Docker as non-root user.

**Planned Module Record:**
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

> **Status:** Logging modules are planned but not yet implemented.

### Log Module (`cyborg.modules.log.v1`) 🚧

> **Status:** Planned - not yet implemented

Writes a structured log message.

**Planned Module Record:**
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
