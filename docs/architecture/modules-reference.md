# Module Reference

Reference documentation for all Cyborg modules, organized by category.

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Common Properties](#common-properties)
  - [Module Base Properties](#module-base-properties)
  - [Module Context](#module-context)
  - [Environment Scoping](#environment-scoping)
  - [Artifacts](#artifacts)
- [Control Flow Modules](#control-flow-modules)
  - [Sequence (`cyborg.modules.sequence.v1`)](#sequence-cyborgmodulessequencev1)
  - [ForEach (`cyborg.modules.foreach.v1`)](#foreach-cyborgmodulesforeachv1)
  - [Guard (`cyborg.modules.guard.v1`)](#guard-cyborgmodulesguardv1)
  - [If (`cyborg.modules.if.v1`)](#if-cyborgmodulesifv1)
  - [Assert (`cyborg.modules.assert.v1`)](#assert-cyborgmodulesassertv1)
  - [Switch (`cyborg.modules.switch.v1`)](#switch-cyborgmodulesswitchv1)
  - [Dynamic (`cyborg.modules.dynamic.v1`)](#dynamic-cyborgmodulesdynamicv1)
- [Condition Modules](#condition-modules)
  - [IsTrue (`cyborg.modules.if.condition.is_true.v1`)](#istrue-cyborgmodulesifconditionis_truev1)
  - [IsSet (`cyborg.modules.if.condition.is_set.v1`)](#isset-cyborgmodulesifconditionis_setv1)
- [Execution Modules](#execution-modules)
  - [Subprocess (`cyborg.modules.subprocess.v1`)](#subprocess-cyborgmodulessubprocessv1)
  - [External (`cyborg.modules.external.v1`)](#external-cyborgmodulesexternalv1)
  - [Template (`cyborg.modules.template.v1`)](#template-cyborgmodulestemplatev1)
- [Configuration Modules](#configuration-modules)
  - [ConfigMap (`cyborg.modules.config.map.v1`)](#configmap-cyborgmodulesconfigmapv1)
  - [ConfigCollection (`cyborg.modules.config.collection.v1`)](#configcollection-cyborgmodulesconfigcollectionv1)
  - [ExternalConfig (`cyborg.modules.config.external.v1`)](#externalconfig-cyborgmodulesconfigexternalv1)
- [Environment Modules](#environment-modules)
  - [Environment Definitions (`cyborg.modules.environment.defs.v1`)](#environment-definitions-cyborgmodulesenvironmentdefsv1)
  - [Named Reference (`cyborg.modules.named.ref.v1`)](#named-reference-cyborgmodulesnamedrefv1)
- [File System Modules](#file-system-modules)
  - [Glob (`cyborg.modules.glob.v1`)](#glob-cyborgmodulesglobv1)
- [Network Modules](#network-modules)
  - [Wake-on-LAN (`cyborg.modules.network.wol.v1`)](#wake-on-lan-cyborgmodulesnetworkwolv1)
  - [SSH Shutdown (`cyborg.modules.network.ssh_shutdown.v1`)](#ssh-shutdown-cyborgmodulesnetworkssh_shutdownv1)
- [Borg Modules](#borg-modules)
  - [Borg Create (`cyborg.modules.borg.create.v1`)](#borg-create-cyborgmodulesborgcreatev1)
  - [Borg Prune (`cyborg.modules.borg.prune.v1`)](#borg-prune-cyborgmodulesborgprunev1)
  - [Borg Compact (`cyborg.modules.borg.compact.v1`)](#borg-compact-cyborgmodulesborgcompactv1)

<!-- /code_chunk_output -->

---

## Common Properties

### Module Base Properties

All modules inherit the following properties from `ModuleBase`:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `name` | string | No | `null` | Optional identifier. Named modules are registered in the module registry and can be referenced by the Named Reference module. |
| `group` | string | No | `null` | Optional grouping tag for organizational purposes. |
| `artifacts` | object | No | See below | Controls how module results are published to the environment. |

### Module Context

Modules are not invoked directly. They are wrapped in a **module context** which pairs the module definition with its execution environment, optional configuration, and template metadata. The context is the standard unit of composition throughout the system.

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `module` | module reference | Yes | -- | The module to execute. |
| `environment` | object | No | `{ "scope": "inherit_parent" }` | Environment scoping for this execution. See [Environment Scoping](#environment-scoping). |
| `configuration` | module reference | No | `null` | A configuration module to execute before the main module. Must implement the configuration module interface. |
| `template` | object | No | `{ "namespace": null, "arguments": [] }` | Template metadata: a `namespace` string and an `arguments` list of expected parameter names. |

### Environment Scoping

The `environment` property on a module context controls variable scope inheritance:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `scope` | enum | No | `inherit_parent` | Scoping strategy. One of: `inherit_parent`, `isolated`, `global`, `inherit_global`, `parent`, `reference`, `current`. |
| `name` | string | No | `null` | Optional scope name. Required for `reference` scope; used to create named scopes with other strategies. |
| `transient` | bool | No | `false` | Whether the scope is transient (not persisted beyond execution). |

### Artifacts

The `artifacts` property controls how a module's execution results are decomposed and published into the environment.

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `namespace` | string | No | `null` | Prefix for published artifact variable names. |
| `exit_status_name` | string | No | `"$?"` | Variable name under which the module's exit status is stored. |
| `environment` | object | No | `{ "scope": "parent" }` | Environment scope for artifact publication. Uses the same scoping model as module contexts but defaults to `parent`. |
| `decomposition_strategy` | enum | No | `leaves_only` | How result objects are flattened: `leaves_only`, `shallow`, or `full_hierarchy`. |
| `publish_null_values` | bool | No | `false` | Whether null-valued result properties are published. |

---

## Control Flow Modules

### Sequence (`cyborg.modules.sequence.v1`)

Executes a list of child modules in order.

**Properties:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `steps` | array of module contexts | Yes | -- | Minimum 1 element | Ordered list of modules to execute. |

**Behavior:**

- Executes each step sequentially.
- If any step returns `Canceled` or `Failed`, execution aborts immediately with that status.
- Returns `Success` if at least one step succeeds; `Skipped` if all steps are skipped.

---

### ForEach (`cyborg.modules.foreach.v1`)

Iterates over a collection variable, executing a body module for each item.

**Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `collection` | string | Yes | -- | Name of an environment variable containing an iterable collection. |
| `item_variable` | string | Yes | -- | Variable name to bind the current item to in each iteration. |
| `continue_on_error` | bool | No | `false` | When `true`, continues iteration even if an item fails. |
| `body` | module context | Yes | -- | Module to execute for each collection item. |

**Behavior:**

- Resolves the `collection` variable from the current environment.
- For each item, creates a scoped environment and binds the item to `item_variable`. If the item supports decomposition (e.g., a structured record), its properties are published hierarchically (e.g., `current_host.hostname`, `current_host.port`).
- If `continue_on_error` is `false` (default), a failed iteration aborts the loop immediately.
- Returns `Success` if at least one iteration succeeded; `Skipped` if all were skipped.

---

### Guard (`cyborg.modules.guard.v1`)

Provides try/catch/finally semantics, guaranteeing cleanup execution.

**Properties:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `try` | module context | Yes | -- | -- | Primary module to execute. |
| `catch` | module context | No | `null` | -- | Module to execute if `try` fails. |
| `finally` | module context | Yes | -- | -- | Module that always executes, regardless of outcome. |
| `behavior` | enum | No | `rethrow` | `rethrow` or `swallow` | How to handle errors when no `catch` is defined. |

**Behavior:**

1. Executes the `try` block.
2. If `try` fails or throws:
   - If `catch` is defined, executes it and uses its status.
   - If no `catch` and `behavior` is `swallow`, resolves as `Success`.
   - If no `catch` and `behavior` is `rethrow`, resolves as `Failed`.
3. If `catch` itself fails after a `try` failure, returns `Failed` immediately (prevents double-handling).
4. The `finally` block always executes (unless cancelled), regardless of `try`/`catch` outcome.

A common scoping pattern: `try` creates a named environment (e.g., `"backup_session"` with `inherit_parent` scope), while `catch` and `finally` use `reference` scope to access that same environment.

---

### If (`cyborg.modules.if.v1`)

Conditionally executes a branch based on a condition module's result.

**Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `condition` | module reference | Yes | -- | A condition module that produces a boolean result. Must be a module returning a `ConditionalResult`. |
| `then` | module context | Yes | -- | Module to execute when the condition is `true`. |
| `else` | module context | No | `null` | Module to execute when the condition is `false`. |

**Behavior:**

- Evaluates the `condition` in an isolated environment.
- If the condition module fails, its status is propagated (the branches are not evaluated).
- If the result is `true`, executes `then`; if `false`, executes `else` (or returns `Skipped` if `else` is not defined).

See [Condition Modules](#condition-modules) for built-in conditions.

---

### Assert (`cyborg.modules.assert.v1`)

Validates a condition and fails with a diagnostic message if the assertion is false.

**Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `assertion` | module reference | Yes | -- | A condition module that produces a boolean result. |
| `message` | string | Yes | -- | Failure message. Supports variable interpolation. |

**Behavior:**

- Executes the `assertion` module. If the assertion module itself fails, its status is propagated.
- If the condition evaluates to `false`, returns `Failed` with the interpolated `message`.
- If `true`, returns `Success`.

**Result:** Publishes an `AssertModuleResult` with the `message` property on failure.

---

### Switch (`cyborg.modules.switch.v1`)

Dispatches execution to one of several named cases based on an environment variable's value.

**Properties:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `variable` | string | Yes | -- | -- | Name of the variable to resolve from the global runtime environment. |
| `cases` | array of case references | Yes | -- | Minimum 1 element | Named cases mapping values to external module files. |

Each case has:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | string | Yes | The value to match against the resolved variable. |
| `path` | string | Yes | File path to the module configuration to load for this case. |

**Behavior:**

- Resolves `variable` from the global runtime environment.
- Finds the case whose `name` matches the resolved value.
- Loads and executes the matched module configuration from the case's `path`.
- Throws if the variable cannot be resolved or no case matches.

---

### Dynamic (`cyborg.modules.dynamic.v1`)

Executes a child module context, allowing the target to be replaced at runtime via environment overrides.

**Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `target` | module context | Yes | -- | The module context to execute. Can be overridden via the environment using the `@<name>.target` convention. |
| `tags` | array of strings | No | `null` | Override resolution tags applied to the child environment, controlling which overrides are selected. |

**Behavior:**

- Prepares a scoped environment for the `target` module context, applying any `tags` for override resolution.
- Executes the resolved target module.
- Returns the target module's status.

---

## Condition Modules

Condition modules are used with `If` and `Assert`. They produce a `ConditionalResult` containing a boolean `result` property.

### IsTrue (`cyborg.modules.if.condition.is_true.v1`)

Checks whether an environment variable resolves to a boolean `true` value.

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `variable` | string | Yes | Name of the environment variable to evaluate as a boolean. |

**Behavior:**

- Resolves the variable from the environment as a boolean.
- Returns `Failed` if the variable is undefined or cannot be interpreted as a boolean.
- Otherwise returns `Success` with the resolved boolean value.

---

### IsSet (`cyborg.modules.if.condition.is_set.v1`)

Checks whether an environment variable is defined, regardless of its value.

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `variable` | string | Yes | Name of the environment variable to check. |

**Behavior:**

- Returns `true` if the variable is defined in the environment, `false` otherwise.
- Always succeeds.

---

## Execution Modules

### Subprocess (`cyborg.modules.subprocess.v1`)

Executes an external process with optional impersonation and output capture.

**Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `command` | object | Yes | -- | The command to execute. See Command below. |
| `output` | object | No | `{ "read_stdout": false, "read_stderr": false }` | Controls stdout/stderr capture. |
| `check_exit_code` | bool | No | `true` | When `true`, a non-zero exit code results in `Failed` status. |
| `impersonation` | object | No | `null` | Run the command as a different user. See Impersonation below. |
| `environment_variables` | array | No | `null` | Process-level environment variables to set. Each entry has `key` and `value` (both strings, both required). |

**Command:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `executable` | string | Yes | Must exist on disk | Path to the executable. |
| `arguments` | array of strings | Yes | -- | Command-line arguments. |

**Output Options:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `read_stdout` | bool | `false` | Capture standard output. |
| `read_stderr` | bool | `false` | Capture standard error. |

**Impersonation:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `executable` | string | No | `"/usr/bin/runuser"` | Must exist on disk | Path to the user-switching utility. |
| `user` | string | Yes | -- | -- | User to run the command as. |

**Behavior:**

- When `impersonation` is set, wraps the command with `runuser -u <user> -- <executable> <args>`.
- Captures stdout/stderr based on `output` settings.
- If `check_exit_code` is `true` (default) and the process exits with a non-zero code, returns `Failed`.

**Result:** Publishes a `SubprocessModuleResult` with `exit_code`, `stdout`, and `stderr` properties.

---

### External (`cyborg.modules.external.v1`)

Loads and executes a module definition from an external JSON file.

**Properties:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `path` | string | Yes | Must exist on disk | Path to the JSON module configuration file. |

**Behavior:**

- Loads the module configuration from `path` and executes it.
- Returns the loaded module's execution status.

---

### Template (`cyborg.modules.template.v1`)

Loads and executes an external module, injecting namespaced arguments into the environment.

**Properties:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `namespace` | string | Yes | Must match `^[A-Za-z0-9_](\.[A-Za-z0-9_\-]+)*$` | Prefix for argument variables. |
| `path` | string | Yes | Must exist on disk | Path to the template module configuration file. |
| `arguments` | array of key-value pairs | No | -- | Typed arguments to inject, scoped under the namespace. |

**Behavior:**

- For each argument, sets a variable named `<namespace>.<key>` in the current environment (e.g., with namespace `backup.overleaf` and key `container_name`, the variable `backup.overleaf.container_name` is set).
- Loads and executes the module at `path`.
- Returns the loaded module's execution status.

---

## Configuration Modules

Configuration modules are used in the `configuration` property of a module context. They execute before the main module and set up environment variables in the current scope.

### ConfigMap (`cyborg.modules.config.map.v1`)

Sets typed key-value pairs in the current environment.

**Properties:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `entries` | array of key-value pairs | Yes | Minimum 1 element | Key-value pairs to set. Values are strongly typed through the dynamic value provider system (e.g., `"string"`, `"int"`, `"bool"`, registered custom types). |

**Example entry formats:**

```json
{ "key": "name", "string": "overleaf" }
{ "key": "port", "int": 22 }
{ "key": "enabled", "bool": true }
```

**Behavior:**

- Sets each key-value pair in the current runtime environment.
- Always returns `Success`.

---

### ConfigCollection (`cyborg.modules.config.collection.v1`)

Aggregates multiple configuration sources into a single configuration block.

**Properties:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `sources` | array of module references | Yes | Minimum 1 element | Configuration modules to execute in order. Each source must be a configuration module. |

**Behavior:**

- Executes each source sequentially in the current environment.
- If any source returns `Canceled` or `Failed`, aborts immediately with that status.
- Returns `Success` if at least one source succeeds; `Skipped` if all are skipped.

---

### ExternalConfig (`cyborg.modules.config.external.v1`)

Loads and executes a configuration module from an external JSON file.

**Properties:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `path` | string | Yes | Must exist on disk | Path to the JSON configuration file. |

**Behavior:**

- Loads the configuration module from `path` and executes it in the current environment (no new scope is created).
- Returns the loaded module's execution status.

---

## Environment Modules

### Environment Definitions (`cyborg.modules.environment.defs.v1`)

Pre-creates named environment scopes for later reference by other modules.

**Properties:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `environments` | array of environment definitions | Yes | Minimum 1 element | Environment scopes to create. Each entry follows the standard [environment scoping](#environment-scoping) model. |

**Behavior:**

- Creates each named environment scope.
- Returns `Success`.

This is useful when multiple modules need to share a named scope that must be created ahead of time (e.g., for `reference` scope access in `Guard` blocks).

---

### Named Reference (`cyborg.modules.named.ref.v1`)

Executes a module that was registered in the module registry by name.

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `target` | string | Yes | The registered name of the module to execute. |

**Behavior:**

- Looks up the module by `target` in the module registry. Modules are registered when they specify a `name` property during configuration loading.
- Executes the resolved module and returns its status.
- Throws if no module with the given name is found.

---

## File System Modules

### Glob (`cyborg.modules.glob.v1`)

Matches files in a directory using a regex pattern.

**Properties:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `pattern` | string | Yes | -- | Valid regex | Case-insensitive regular expression to match file names. |
| `root` | string | Yes | -- | Must exist on disk (directory) | Root directory to search. |
| `recurse` | bool | No | `false` | -- | Whether to search subdirectories. |

**Behavior:**

- Enumerates files in `root` (recursively if `recurse` is `true`).
- Filters file names against the regex `pattern`.
- Returns `Success`.

**Result:** Publishes a `GlobModuleResult` with a `files` collection of matching file paths.

---

## Network Modules

### Wake-on-LAN (`cyborg.modules.network.wol.v1`)

Sends a Wake-on-LAN magic packet and waits for the target host to become reachable.

**Properties:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `target_host` | string | Yes | -- | -- | Hostname or IP of the target. |
| `mac_address` | string | Yes | -- | Format: `XX:XX:XX:XX:XX:XX` or `XX-XX-XX-XX-XX-XX` | MAC address of the target NIC. |
| `liveness_probe_port` | int | Yes | -- | 1 -- 65535 | TCP port to probe for host readiness. |
| `max_wait_time` | timespan | No | `"00:05:00"` | -- | Maximum time to wait for the host to come online. |
| `host_discovery_timeout` | timespan | No | `"00:00:30"` | -- | Timeout for the initial reachability check (ping). |
| `executable` | string | No | `"/usr/bin/wakeonlan"` | Must exist on disk | Path to the wakeonlan utility. |

**Behavior:**

1. Pings the target host with `host_discovery_timeout`.
2. If already reachable, returns `Success` (no wake needed).
3. If unreachable, sends a WoL packet via the wakeonlan utility.
4. Probes `liveness_probe_port` repeatedly until the host responds or `max_wait_time` expires.
5. Returns `Failed` if the host does not come online within the timeout.

**Result:** Publishes a `WakeOnLanModuleResult` with a `woke_up` boolean indicating whether a wake was actually performed.

---

### SSH Shutdown (`cyborg.modules.network.ssh_shutdown.v1`)

Shuts down a remote host by executing a command over SSH.

**Properties:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `executable` | string | No | `"/usr/bin/ssh"` | Must exist on disk | Path to the SSH client. |
| `hostname` | string | Yes | -- | -- | Target host. |
| `username` | string | Yes | -- | -- | SSH user. |
| `port` | int | No | `22` | 1 -- 65535 | SSH port. |
| `shutdown_command` | string | No | `"/usr/bin/shutdown -h now"` | -- | Remote command to execute for shutdown. |
| `ssh_pass` | object | No | `null` | -- | Optional sshpass configuration for passphrase-based authentication. |

**SshPass:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `executable` | string | No | `"/usr/bin/sshpass"` | Must exist on disk | Path to sshpass. |
| `file_path` | string | Yes | -- | Must exist on disk | File containing the passphrase. |
| `match_prompt` | string | No | `null` | -- | Custom prompt string for sshpass to match (e.g., `"assphrase"`). |

**Behavior:**

- Constructs an SSH command: `ssh <username>@<hostname>:<port> <shutdown_command>`.
- If `ssh_pass` is configured, wraps the command with sshpass for non-interactive authentication.
- Returns `Failed` on non-zero exit code, `Success` otherwise.

**Result:** Publishes an `SshShutdownModuleResult` with `exit_code`, `standard_output`, and `standard_error` properties.

---

## Borg Modules

Borg modules integrate with [BorgBackup](https://borgbackup.readthedocs.io/) for repository management. They share a common set of properties inherited from a Borg-specific base type, in addition to the standard [module base properties](#module-base-properties).

**Shared Borg Properties:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `executable` | string | No | `"/usr/bin/borg"` | Must exist on disk | Path to the borg binary. |
| `passphrase` | string | Yes | -- | -- | Repository passphrase (set as `BORG_PASSPHRASE`). |
| `remote_shell` | object | No | `null` | -- | SSH transport options. See Remote Shell below. |
| `remote_repository` | object | Yes | -- | -- | Remote repository connection details. See Remote Repository below. |

**Remote Shell (`remote_shell`):**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `executable` | string | No | `"/usr/bin/ssh"` | Must exist on disk | Path to the SSH client. |
| `ssh_pass` | object | No | `null` | -- | Optional sshpass configuration (same structure as SSH Shutdown's `ssh_pass`: `executable`, `file_path`, `match_prompt`). |

When `remote_shell` is set, constructs the `BORG_RSH` environment variable for the borg process.

**Remote Repository (`remote_repository`):**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `protocol` | string | No | `"ssh://"` | -- | Repository URI protocol. |
| `username` | string | Yes | -- | -- | Remote user. |
| `hostname` | string | Yes | -- | -- | Remote host. |
| `port` | int | Yes | -- | 1 -- 65535 | Remote port. |
| `repository_root` | string | No | `null` | -- | Base path on the remote host. |
| `repository_name` | string | Yes | -- | -- | Repository name. |

The repository URI is constructed as `<protocol><username>@<hostname>:<port><repository_root>/<repository_name>`.

---

### Borg Create (`cyborg.modules.borg.create.v1`)

Creates a new archive in a borg repository.

**Properties** (in addition to shared Borg properties):

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `archive_name` | string | Yes | -- | -- | Name for the new archive. |
| `source_path` | string | Yes | -- | Must exist on disk (directory) | Directory to back up. |
| `compression` | string | No | `"lz4"` | Must match borg compression grammar: `none`, `lz4`, `zstd[,1-22]`, `zlib[,0-9]`, `lzma[,0-9]`, or `auto,<method>` | Compression algorithm. |
| `exclude` | object | No | `{ "caches": false, "paths": [] }` | -- | Exclusion options. |

**Exclude Options:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `caches` | bool | `false` | Exclude directories tagged as caches. |
| `paths` | array of strings | `[]` | File patterns to exclude. |

**Behavior:**

- Runs `borg create` with `--stats --json --compression <compression>`.
- Applies exclusion flags and paths if configured.
- Returns `Failed` on non-zero exit code, `Success` otherwise.

---

### Borg Prune (`cyborg.modules.borg.prune.v1`)

Prunes old archives from a borg repository based on retention rules.

**Properties** (in addition to shared Borg properties):

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `glob_archives` | string | No | `null` | -- | Glob pattern to select which archives to consider for pruning. |
| `keep` | object | Yes | -- | -- | Retention rules. See Keep Rules below. |
| `save_space` | bool | No | `false` | -- | Trade speed for lower disk usage during pruning. |
| `checkpoint_interval` | timespan | No | `"00:30:00"` | Minimum 1 second | Interval between checkpoints. |

**Keep Rules:**

All values are integers. A value of `0` means the rule is not applied.

| Property | Description |
|----------|-------------|
| `last` | Number of most recent archives to keep. |
| `minutely` | Keep one archive per minute for the last N minutes. |
| `hourly` | Keep one archive per hour for the last N hours. |
| `daily` | Keep one archive per day for the last N days. |
| `weekly` | Keep one archive per week for the last N weeks. |
| `monthly` | Keep one archive per month for the last N months. |
| `yearly` | Keep one archive per year for the last N years. |
| `weekly13` | Keep one archive per week for the last 13 weeks (rolling quarter). |
| `monthly3` | Keep one archive per month for the last 3 months (rolling quarter). |

**Behavior:**

- Runs `borg prune` with `--list --log-json` and the configured keep rules (only rules with values > 0 are included).
- Applies `--glob-archives`, `--save-space`, and `--checkpoint-interval` if configured.
- Returns `Failed` on non-zero exit code, `Success` otherwise.

---

### Borg Compact (`cyborg.modules.borg.compact.v1`)

Compacts a borg repository to reclaim disk space freed by pruning.

**Properties** (in addition to shared Borg properties):

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `threshold` | int | No | `10` | 1 -- 99 | Minimum savings percentage to trigger compaction. |

**Behavior:**

- Runs `borg compact --threshold <threshold>`.
- Returns `Failed` on non-zero exit code, `Success` otherwise.
