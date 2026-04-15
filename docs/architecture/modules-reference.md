# Module Reference

This document covers the configuration and behavior of all Cyborg modules. Modules are the building blocks of Cyborg workflows: each module is a self-contained unit that performs a specific task, and modules compose together through the module context system to form arbitrarily complex execution graphs.

Every module is identified by a versioned ID (e.g., `cyborg.modules.sequence.v1`) which serves as both the JSON discriminator key and the version identifier. Modules communicate through a hierarchical environment of typed variables, where each module can read from and publish to scoped environments. Module properties support runtime overrides from the environment, enabling data-driven composition patterns.

For details on the execution model, environment scoping semantics, variable resolution, the property override system, and artifact publishing, see [Runtime Infrastructure](../architecture.md).

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
  - [While (`cyborg.modules.while.v1`)](#while-cyborgmoduleswhilev1)
  - [Assert (`cyborg.modules.assert.v1`)](#assert-cyborgmodulesassertv1)
  - [Switch (`cyborg.modules.switch.v1`)](#switch-cyborgmodulesswitchv1)
  - [Dynamic (`cyborg.modules.dynamic.v1`)](#dynamic-cyborgmodulesdynamicv1)
  - [Empty (`cyborg.modules.empty.v1`)](#empty-cyborgmodulesemptyv1)
- [Condition Modules](#condition-modules)
  - [IsTrue (`cyborg.modules.condition.is_true.v1`)](#istrue-cyborgmodulesconditionis_truev1)
  - [IsSet (`cyborg.modules.condition.is_set.v1`)](#isset-cyborgmodulesconditionis_setv1)
  - [And (`cyborg.modules.condition.and.v1`)](#and-cyborgmodulesconditionandv1)
  - [Or (`cyborg.modules.condition.or.v1`)](#or-cyborgmodulesconditionorv1)
  - [Not (`cyborg.modules.condition.not.v1`)](#not-cyborgmodulesconditionnotv1)
  - [FileExists (`cyborg.modules.condition.file_exists.v1`)](#fileexists-cyborgmodulesconditionfile_existsv1)
  - [DirectoryExists (`cyborg.modules.condition.directory_exists.v1`)](#directoryexists-cyborgmodulesconditiondirectory_existsv1)
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
  - [Borg v1.4.X Modules](#borg-v14x-modules)
    - [Borg Create (`cyborg.modules.borg.create.v1.4`)](#borg-create-cyborgmodulesborgcreatev14)
    - [Borg Prune (`cyborg.modules.borg.prune.v1.4`)](#borg-prune-cyborgmodulesborgprunev14)
    - [Borg Compact (`cyborg.modules.borg.compact.v1.4`)](#borg-compact-cyborgmodulesborgcompactv14)

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
| `requires` | object | No | `{ "argument_namespace": null, "arguments": [] }` | Module requirements: an `argument_namespace` string and an `arguments` list of expected parameter names. Used to declare required environment variables that must be present before execution. |

When a module context is executed, the runtime first prepares an environment according to the `environment` settings, then executes the `configuration` module (if present) to populate that environment, and finally executes the main `module` within it. This sequencing ensures that configuration values are available to the main module and that environment scoping is fully established before any work begins.

### Environment Scoping

The `environment` property on a module context controls variable scope inheritance:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `scope` | enum | No | `inherit_parent` | Scoping strategy, see [Runtime Infrastructure -- Environment Scoping](./architecture-overview.md#environment-scoping) for available strategies and detailed semantics. |
| `name` | string | No | `null` | Optional scope name. Required for `reference` scope; used to create named scopes with other strategies. |
| `transient` | bool | No | `false` | Whether the scope is transient (not persisted beyond execution). |

Environments declared with an explicit `name` (and not marked `transient`) are registered globally. Any subsequent module can access them via `reference` scope. This is the primary mechanism for cross-step state sharing. For a detailed overview of environment semantics, see [Runtime Infrastructure -- Environment Scoping](./architecture-overview.md#environment-scoping).

### Artifacts

The `artifacts` property controls how a module's execution results are decomposed and published into the environment. When a module completes, its result object (if any) is decomposed into individual variables and published to a target environment, making the output accessible to subsequent modules via standard variable resolution.

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `namespace` | string | No | Module's effective namespace | Prefix for published artifact variable names. The effective namespace is the module's `name` if set, otherwise its `group`, otherwise the module ID. |
| `exit_status_name` | string | No | `"$?"` | Variable name under which the module's exit status (`Success`, `Failed`, `Skipped`, `Canceled`) is stored. |
| `environment` | object | No | `{ "scope": "parent" }` | Target environment for artifact publication. Uses the same scoping model as module contexts but defaults to `parent`, meaning artifacts flow to the parent module's environment. |
| `decomposition_strategy` | enum | No | `leaves_only` | Controls how deeply result objects are flattened into variables. `leaves_only`: only leaf (non-decomposable) values are published. `shallow`: top-level properties are published. `full_hierarchy`: the root and all nested levels are published. |
| `publish_null_values` | bool | No | `false` | Whether null-valued result properties are published. |

For more details on artifact lifecycle and exposure patterns, see [Runtime Infrastructure -- Artifact Publishing](./architecture-overview.md#artifact-publishing).

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
| `collection` | string | Yes | -- | Name of an environment variable containing an iterable collection. Collections are typically populated via the dynamic value system using the `collection<T>` type syntax in a ConfigMap (e.g., `"collection<cyborg.types.borg.remote.v1.4>"`). |
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
| `finally` | module context | No | `null` | -- | Module that always executes, regardless of outcome. |
| `behavior` | enum | No | `rethrow` | `rethrow` or `swallow` | How to handle errors when no `catch` is defined. |

**Validation:** At least one of `catch` or `finally` must be defined.

**Behavior:**

1. Executes the `try` block.
2. If `try` fails or throws:
   - If `catch` is defined, executes it and uses its status.
   - If no `catch` and `behavior` is `swallow`, resolves as `Success`.
   - If no `catch` and `behavior` is `rethrow`, resolves as `Failed`.
3. If `catch` itself fails after a `try` failure, returns `Failed` immediately (prevents double-handling).
4. The `finally` block always executes (if defined and unless cancelled), regardless of `try`/`catch` outcome.

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
| `invert_condition` | bool | No | `false` | When `true`, swaps the evaluation: executes `else` when the condition is `true` and `then` when `false`. |

**Behavior:**

- Evaluates the `condition` in a child environment with `inherit_parent` scope (inherits parent variables, but writes are isolated to the child scope).
- If the condition module fails, its status is propagated (the branches are not evaluated).
- If `invert_condition` is `false` (default): executes `then` when `true`, `else` when `false`.
- If `invert_condition` is `true`: executes `else` when `true`, `then` when `false`.
- Returns `Skipped` if the selected branch is not defined.

See [Condition Modules](#condition-modules) for built-in conditions.

---

### While (`cyborg.modules.while.v1`)

Repeatedly executes a `body` module as long as a `condition` module evaluates to `true`.

**Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `condition` | module reference | Yes | -- | A condition module that produces a boolean result. Must be a module returning a `ConditionalResult`. |
| `body` | module context | Yes | -- | Module to execute on each iteration while the condition holds. |
| `invert_condition` | bool | No | `false` | When `true`, continues looping while the condition is `false` instead of `true` (i.e., loops until the condition becomes `true`). |

**Behavior:**

- Evaluates the `condition` in a child environment with `inherit_parent` scope (inherits parent variables, but writes are isolated to the child scope) before each iteration.
- If the condition module fails, its status is propagated and the loop aborts.
- If `invert_condition` is `false` (default): continues looping while condition is `true`, exits when `false`.
- If `invert_condition` is `true`: continues looping while condition is `false`, exits when `true`.
- If the `body` module does not succeed, its status is propagated and the loop aborts.
- Returns `Success` when the loop exits normally (condition no longer met).

See [Condition Modules](#condition-modules) for built-in conditions.

---

### Assert (`cyborg.modules.assert.v1`)

Validates a condition and fails with a diagnostic message if the assertion is false. Useful for enforcing preconditions at the start of a workflow (e.g., verifying that required variables are defined before proceeding).

**Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `assertion` | module reference | Yes | -- | A condition module that produces a boolean result. |
| `message` | string | Yes | -- | Failure message. Supports `${...}` variable interpolation, so the message can include resolved environment values for diagnostics. |

**Behavior:**

- Executes the `assertion` module. If the assertion module itself fails, its status is propagated.
- If the condition evaluates to `false`, returns `Failed` with the interpolated `message`.
- If `true`, returns `Success`.

**Result:** Publishes an `AssertModuleResult` with the `message` property on failure.

---

### Switch (`cyborg.modules.switch.v1`)

Dispatches execution to one of several named cases based on an environment variable's value. Each case maps a string value to an external module configuration file that is loaded and executed when matched.

**Properties:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `variable` | string | Yes | -- | -- | Name of the variable to resolve. |
| `cases` | array of case references | Yes | -- | Minimum 1 element | Named cases mapping values to external module files. |

Each case has:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | string | Yes | The value to match against the resolved variable. |
| `path` | string | Yes | File path to the module configuration to load for this case. |

**Behavior:**

- Resolves `variable` from the environment.
- Finds the case whose `name` matches the resolved value.
- Loads and executes the matched module configuration from the case's `path`.
- Throws if the variable cannot be resolved or no case matches.

---

### Dynamic (`cyborg.modules.dynamic.v1`)

Executes a child module context, allowing the target to be replaced at runtime via environment overrides. This module is the primary mechanism for late-bound module composition, where the actual module to execute is determined by the environment state at runtime rather than being statically defined in the configuration.

**Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `target` | module context | Yes | -- | The module context to execute. Like all module properties, this can be overridden from the environment using the `@<name>.target` convention, which is what makes the module "dynamic" -- a parent module can inject a different module context at runtime. |
| `tags` | array of strings | No | `null` | Override resolution tags applied to the child environment. Tags extend the override lookup chain beyond the standard `name` / `group` / `module_id` sequence, allowing ambient overrides keyed by tag to apply to any module executing in that environment. See [Runtime Infrastructure -- Override Resolution Tags](./architecture-overview.md#override-resolution-tags). |

**Behavior:**

- Prepares a scoped environment for the `target` module context, applying any `tags` for override resolution.
- Executes the resolved target module.
- Returns the target module's status.

---

### Empty (`cyborg.modules.empty.v1`)

A no-op module that immediately returns `Success`. Useful as a placeholder or default value when a module is required but no action is needed. May also be used to enforce required environment variables by declaring them in the `requires::arguments` property of the context.

**Properties:*

None.

**Behavior:**

- Returns `Success` immediately.

---

## Condition Modules

Condition modules are used with `If`, `While`, and `Assert`. They produce a `ConditionalResult` containing a boolean `result` property.

### IsTrue (`cyborg.modules.condition.is_true.v1`)

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

### IsSet (`cyborg.modules.condition.is_set.v1`)

Checks whether an environment variable is defined, regardless of its value.

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `variable` | string | Yes | Name of the environment variable to check. |

**Behavior:**

- Returns `true` if the variable is defined in the environment, `false` otherwise.
- Always succeeds.

---

### And (`cyborg.modules.condition.and.v1`)

Evaluates multiple condition modules and returns `true` only if all conditions evaluate to `true`.

**Properties:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `conditions` | array of module references | Yes | Minimum 1 element | Condition modules to evaluate in order. Each must return a `ConditionalResult`. |

**Behavior:**

- Evaluates conditions in order and short-circuits on the first condition that evaluates to `false`.
- Returns `Success` with `result = true` only if every condition evaluates to `true`.
- If any condition returns `Canceled`, returns `Canceled`.
- If any condition returns a non-`Success` status (including `Failed` and `Skipped`) or does not publish a readable boolean `result`, returns `Failed`.

---

### Or (`cyborg.modules.condition.or.v1`)

Evaluates multiple condition modules and returns `true` if any condition evaluates to `true`.

**Properties:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `conditions` | array of module references | Yes | Minimum 1 element | Condition modules to evaluate in order. Each must return a `ConditionalResult`. |

**Behavior:**

- Evaluates conditions in order and short-circuits on the first condition that evaluates to `true`.
- Returns `Success` with `result = false` only if all conditions evaluate to `false`.
- If any condition returns `Canceled`, returns `Canceled`.
- If any condition returns a non-`Success` status (including `Failed` and `Skipped`) or does not publish a readable boolean `result`, returns `Failed`.

---

### Not (`cyborg.modules.condition.not.v1`)

Negates the boolean result of a single condition module.

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `condition` | module reference | Yes | Condition module to evaluate. Must return a `ConditionalResult`. |

**Behavior:**

- Evaluates `condition` and returns `Success` with the inverted boolean result.
- If `condition` returns `Canceled`, returns `Canceled`.
- If `condition` returns a non-`Success` status (including `Failed` and `Skipped`) or does not publish a readable boolean `result`, returns `Failed`.

---

### FileExists (`cyborg.modules.condition.file_exists.v1`)

Checks whether a file exists at a given path.

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `path` | string | Yes | File path to check for existence. |

**Behavior:**

- Returns `true` if the file exists, `false` otherwise.
- Always succeeds.

---

### DirectoryExists (`cyborg.modules.condition.directory_exists.v1`)

Checks whether a directory exists at a given path.

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `path` | string | Yes | Directory path to check for existence. |

**Behavior:**

- Returns `true` if the directory exists, `false` otherwise.
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
| `executable` | string | No | `"/usr/sbin/runuser"` | Must exist on disk | Path to the user-switching utility. |
| `user` | string | Yes | -- | -- | User to run the command as. |

**Behavior:**

- When `impersonation` is set, wraps the command with `runuser -u <user> -- <executable> <args>`.
- Captures stdout/stderr based on `output` settings.
- If `check_exit_code` is `true` (default) and the process exits with a non-zero code, returns `Failed`.

**Result:** Publishes a `SubprocessModuleResult` with `exit_code`, `stdout`, and `stderr` properties. These are available as variables in the artifact target environment (by default, the parent scope) under the module's artifact namespace.

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

Loads and executes an external module, injecting namespaced arguments into the child environment. This is the standard mechanism for reusable module definitions: a template file defines a parameterized workflow, and each invocation supplies its own arguments under a unique namespace.

**Properties:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `namespace` | string | Yes | Must match `^[A-Za-z0-9_]+(\.[A-Za-z0-9_\-]+)*$` | Prefix for argument variables. |
| `path` | string | Yes | Must exist on disk | Path to the template module configuration file. |
| `arguments` | array of key-value pairs | No | -- | Typed arguments to inject, scoped under the namespace. Uses the same dynamic value system as ConfigMap entries (`"string"`, `"int"`, `"bool"`, registered custom types). |
| `overrides` | array of key-value pairs | No | -- | Optional environment overrides to apply when executing the loaded module. Each entry has `key` (string) and `value` (string) properties. The `key` is the override key (e.g., `@my_template.target`) and the `value` is the override value. |

**Behavior:**

- For each argument, sets a variable named `<namespace>.<key>` in the child environment (e.g., with namespace `backup.overleaf` and key `container_name`, the variable `backup.overleaf.container_name` is set).
- For each override, sets the specified override key and value in the child environment's override resolution context.
- Loads and executes the module at `path`. The loaded module's properties can reference template arguments via the standard variable interpolation and override mechanisms (e.g., `${backup.overleaf.container_name}`).
- Returns the loaded module's execution status.

---

## Configuration Modules

Configuration modules are used in the `configuration` property of a module context. They execute before the main module and set up environment variables in the current scope. Unlike regular modules, configuration modules write directly into the current environment rather than creating a child scope, ensuring their variables are visible to the main module they configure.

### ConfigMap (`cyborg.modules.config.map.v1`)

Sets typed key-value pairs in the current environment.

**Properties:**

| Property | Type | Required | Constraints | Description |
|----------|------|----------|-------------|-------------|
| `entries` | array of key-value pairs | Yes | Minimum 1 element | Key-value pairs to set. Values are strongly typed through the dynamic value provider system. |

Each entry is a JSON object with a `key` and exactly one type-tagged value. The type name is a property key that identifies the value provider:

```json
{ "key": "name", "string": "overleaf" }
{ "key": "port", "int": 22 }
{ "key": "enabled", "bool": true }
{ "key": "hosts", "collection<cyborg.types.borg.remote.v1.4>": [{ ... }] }
```

Built-in types include `string`, `int`, `bool`, and `collection<T>`. Custom types register a versioned type name (e.g., `cyborg.types.borg.remote.v1.4`) and are resolved through the dynamic value provider registry. See [Runtime Infrastructure -- Dynamic Value System](./architecture-overview.md#dynamic-value-system) for details.

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

Executes a module that was registered in the module registry by name. Any module that declares a `name` in its [base properties](#module-base-properties) is automatically registered during JSON deserialization and becomes referenceable by this module. This supports defining a module once and invoking it from multiple places without duplicating the configuration.

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `target` | string | Yes | The registered name of the module to execute. |

**Behavior:**

- Looks up the module by `target` in the module registry.
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

Borg modules integrate with [BorgBackup](https://borgbackup.readthedocs.io/) for repository management. They share a common set of properties inherited from a Borg-specific base type, in addition to the standard [module base properties](#module-base-properties). All shared Borg properties support the standard override mechanism, so values like `executable`, `passphrase`, and `remote_repository` can be injected from the environment at runtime -- typically via a ConfigMap or Template at the job level.

### Borg v1.4.X Modules

These modules are designed for Borg v1.4.X, which is the latest stable release series as of this writing. They leverage features and improvements introduced in the 1.4 release, such as enhanced JSON output and new pruning options. Support for older versions of Borg may be added in the future if needed, but v1.4 is recommended for its performance and reliability benefits.

Borg v2.X compatibility will be added in a future release once the 2.0 API stabilizes and we can define a clear set of properties and behaviors for the new version.

**Shared Borg Properties:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `executable` | string | Yes | `"/usr/bin/borg"` | Must exist on disk | Path to the borg binary. |
| `passphrase` | string | Yes | -- | -- | Repository passphrase (set as `BORG_PASSPHRASE`). |
| `remote_shell` | object | No | `null` | -- | SSH transport options. See Remote Shell below. |
| `remote_repository` | object | Yes | -- | -- | Remote repository connection details. See Remote Repository below. |

**Remote Shell (`remote_shell`):**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `executable` | string | Yes | `"/usr/bin/ssh"` | Must exist on disk | Path to the SSH client. |
| `ssh_pass` | object | No | `null` | -- | Optional sshpass configuration (same structure as SSH Shutdown's `ssh_pass`: `executable`, `file_path`, `match_prompt`). |

When `remote_shell` is set, constructs the `BORG_RSH` environment variable for the borg process.

**Remote Repository (`remote_repository`):**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `protocol` | string | Yes | `"ssh://"` | -- | Repository URI protocol. |
| `username` | string | Yes | -- | -- | Remote user. |
| `hostname` | string | Yes | -- | -- | Remote host. |
| `port` | int | Yes | -- | 1 -- 65535 | Remote port. |
| `repository_root` | string | No | `null` | -- | Base path on the remote host. |
| `repository_name` | string | Yes | -- | -- | Repository name. |

The repository URI is constructed as `<protocol><username>@<hostname>:<port><repository_root>/<repository_name>`.

---

#### Borg Create (`cyborg.modules.borg.create.v1.4`)

Creates a new archive in a borg v1.4.X repository.

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

#### Borg Prune (`cyborg.modules.borg.prune.v1.4`)

Prunes old archives from a borg v1.4.X repository based on retention rules.

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

#### Borg Compact (`cyborg.modules.borg.compact.v1.4`)

Compacts a borg v1.4.X repository to reclaim disk space freed by pruning.

**Properties** (in addition to shared Borg properties):

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `threshold` | int | No | `10` | 1 -- 99 | Minimum savings percentage to trigger compaction. |

**Behavior:**

- Runs `borg compact --threshold <threshold>`.
- Returns `Failed` on non-zero exit code, `Success` otherwise.
