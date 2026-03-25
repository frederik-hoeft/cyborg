# Templates Reference

This document covers reusable workflow templates shipped as external Cyborg configuration files rather than built-in modules. Templates are invoked through the [`cyborg.modules.template.v1`](modules-reference.md#template-cyborgmodulestemplatev1) module and usually expose a narrow, usage-oriented argument surface while hiding the internal workflow structure.

For runtime behavior (environment scoping, variable resolution, override semantics, and artifacts), see [Runtime Infrastructure](runtime.md). For the Template module itself, see [Module Reference](modules-reference.md#template-cyborgmodulestemplatev1). For the dynamic argument types used below, see [Dynamic Values Reference](dynamic-values-reference.md).

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Docker Backup Template (`cyborg.template.backup-job.docker.v1`)](#docker-backup-template-cyborgtemplatebackup-jobdockerv1)
  - [Purpose](#purpose)
  - [Invocation Shape](#invocation-shape)
  - [Required Arguments](#required-arguments)
  - [Derived Variables](#derived-variables)
  - [Behavior](#behavior)
  - [Expected Secrets File](#expected-secrets-file)
  - [Injected Borg Overrides](#injected-borg-overrides)
  - [Override Surface Exposed by the Template](#override-surface-exposed-by-the-template)
    - [Docker task overrides](#docker-task-overrides)
    - [Borg task overrides](#borg-task-overrides)
  - [Usage Pattern](#usage-pattern)
  - [Example: Basic Job](#example-basic-job)
  - [Example: Custom Docker Compose Commands](#example-custom-docker-compose-commands)
- [Systemd Backup Template (`cyborg.template.backup-job.systemd.v1`)](#systemd-backup-template-cyborgtemplatebackup-jobsystemdv1)
  - [Purpose](#purpose-1)
  - [Invocation Shape](#invocation-shape-1)
  - [Required Arguments](#required-arguments-1)
  - [Derived Variables](#derived-variables-1)
  - [Behavior](#behavior-1)
  - [Expected Secrets File](#expected-secrets-file-1)
  - [Injected Borg Overrides](#injected-borg-overrides-1)
  - [Override Surface Exposed by the Template](#override-surface-exposed-by-the-template-1)
    - [Systemd task overrides](#systemd-task-overrides)
    - [Borg task overrides](#borg-task-overrides-1)
  - [Usage Pattern](#usage-pattern-1)
  - [Example: Basic Job](#example-basic-job-1)
  - [Example: Custom systemctl Commands](#example-custom-systemctl-commands)
- [Compatibility Notes](#compatibility-notes)

<!-- /code_chunk_output -->

---

## Docker Backup Template (`cyborg.template.backup-job.docker.v1`)

### Purpose

This template implements the common workflow for backing up a Dockerized application:

1. stop the container,
2. run `borg create`, `borg prune`, and `borg compact` for each configured backup host, and
3. start the container again in a `finally` block.

The template is intended to be reused by concrete job files such as `overleaf.jconf` and `passbolt.jconf`, which provide the application-specific arguments and the concrete Borg module definitions.

The template deliberately treats the three Borg operations as **modules supplied as data**. Callers provide the concrete `borg_create`, `borg_prune`, and `borg_compact` module contexts, while the template injects the per-host repository and transport settings at runtime.

### Invocation Shape

Invoke the template through `cyborg.modules.template.v1` using the namespace declared by the template itself:

```json
{
  "cyborg.modules.template.v1": {
    "namespace": "cyborg.template.backup-job.docker.v1",
    "path": "${cyborg_template_root}/docker-backup-template.jconf",
    "arguments": [
      { "key": "container_name", "string": "passbolt" },
      { "key": "docker_root", "string": "/srv/docker" },
      { "key": "docker_user", "string": "docker" },
      { "key": "borg_passphrase", "string": "${some_secret}" },
      {
        "key": "backup_hosts",
        "collection<cyborg.types.borg.remote.v1>": [
          { "hostname": "nas-01", "port": 22, "borg_repo_root": "/srv/borg", "borg_user": "borg", "remote_shell": { "executable": "/usr/bin/ssh" } }
        ]
      },
      { "key": "job_directory", "string": "/etc/cyborg/jobs" },
      { "key": "borg_create", "cyborg.types.module.context.v1": { "module": { "cyborg.modules.borg.create.v1": { } } } },
      { "key": "borg_prune", "cyborg.types.module.context.v1": { "module": { "cyborg.modules.borg.prune.v1": { } } } },
      { "key": "borg_compact", "cyborg.types.module.context.v1": { "module": { "cyborg.modules.borg.compact.v1": { } } } }
    ]
  }
}
```

The declared template namespace is part of the API surface. Callers should treat `cyborg.template.backup-job.docker.v1` as fixed for this template version.

### Required Arguments

The template declares the following required arguments:

| Argument | Type | Description |
|----------|------|-------------|
| `container_name` | `string` | Logical container/application name. Also used as the Borg repository name. |
| `docker_root` | `string` | Root directory containing the template's expected Docker layout. |
| `borg_passphrase` | `string` | Borg repository passphrase injected into the Borg tasks. |
| `docker_user` | `string` | User used for Docker subprocess impersonation. |
| `backup_hosts` | `collection<cyborg.types.borg.remote.v1>` | Backup targets processed sequentially. Each host provides `hostname`, `port`, `borg_user`, `borg_repo_root`, and `remote_shell`. |
| `job_directory` | `string` | Base directory for job-specific companion files. Used to load `${job_directory}/${container_name}.jsecrets`. |
| `borg_create` | `cyborg.types.module.context.v1` | Module context executed for archive creation. Intended to be a Borg Create module. |
| `borg_prune` | `cyborg.types.module.context.v1` | Module context executed for retention/pruning. Intended to be a Borg Prune module. |
| `borg_compact` | `cyborg.types.module.context.v1` | Module context executed for compaction. Intended to be a Borg Compact module. |

In normal usage, the caller only supplies the operation-specific properties on the Borg modules (for example `archive_name`, `source_path`, retention rules, excludes, or compact threshold). The template injects the shared connection details per host.

### Derived Variables

Before executing the main workflow, the template creates a small set of derived variables in its configuration phase:

| Variable | Value | Purpose |
|----------|-------|---------|
| `container_root` | `${docker_root}/containers/${container_name}` | Default Docker Compose location used by the stop/start subprocesses. |
| `volume_root` | `${docker_root}/volumes/${container_name}` | Default data root typically used as the Borg create source path. |
| `host` | current element from `backup_hosts` | Per-iteration host object exposed by the `ForEach` body. |

These variables are intentionally part of the practical usage surface because the injected Borg module definitions commonly reference them. For example, `source_path` is usually set to `${volume_root}` and `archive_name` often uses `${container_name}`.

### Behavior

At runtime, the template behaves as follows:

1. Executes its configuration block.
   - Creates `container_root` and `volume_root`.
   - Wires the caller-supplied module contexts into the internal dynamic modules.
   - Injects shared Borg overrides for all backup hosts.
   - Loads the job-specific secrets/configuration file.
2. Executes `docker_tasks.down`.
3. Enters a `Guard` block whose `finally` always executes `docker_tasks.up`.
4. Inside the `try` block, iterates `backup_hosts` in order.
5. For each host, executes the supplied `borg_create`, `borg_prune`, and `borg_compact` module contexts in sequence.
6. If any step in the host loop fails, the loop aborts immediately and the failure propagates after container startup has been attempted in `finally`.

The template therefore guarantees container restart on the normal failure path of the backup sequence, while still surfacing the original backup failure to the caller.

### Expected Secrets File

During the configuration phase, the template loads:

```text
${job_directory}/${container_name}.jsecrets
```

This file is loaded through `cyborg.modules.config.external.v1`, so it must contain a **configuration module**, not an arbitrary data file. In practice this usually means a `cyborg.modules.config.map.v1` or `cyborg.modules.config.collection.v1` document.

The secrets/configuration file is executed after `container_root` and `volume_root` have been defined, so it may reference those values. If the file is missing or invalid, template execution fails before the Docker stop/start workflow begins.

### Injected Borg Overrides

The template applies the `borg_tasks` override tag to all three dynamically executed Borg operations and injects the following values for each current `host`:

| Override Key | Effective Value |
|--------------|-----------------|
| `@borg_tasks.remote_shell` | `${@host.remote_shell}` |
| `@borg_tasks.passphrase` | `${borg_passphrase}` |
| `@borg_tasks.remote_repository.username` | `${@host.borg_user}` |
| `@borg_tasks.remote_repository.hostname` | `${@host.hostname}` |
| `@borg_tasks.remote_repository.repository_root` | `${@host.borg_repo_root}` |
| `@borg_tasks.remote_repository.repository_name` | `${container_name}` |
| `@borg_tasks.remote_repository.port` | `${@host.port}` |

This is the main reason the caller does **not** need to repeat host-specific repository settings in `borg_create`, `borg_prune`, and `borg_compact`.

The intended contract is that the caller supplies Borg module contexts whose remaining properties describe *what* to do, while the template supplies *where* to do it for each host.

### Override Surface Exposed by the Template

In addition to its required arguments, the template exposes a deliberate override surface through internal module names, groups, and tags.

#### Docker task overrides

The stop/start subprocess modules use:

- group: `docker_tasks`
- names: `docker_tasks.down` and `docker_tasks.up`

This makes the following patterns possible:

- `@docker_tasks.command.executable` — override the executable for both stop and start.
- `@docker_tasks.impersonation.user` — override the impersonated user for both stop and start.
- `@docker_tasks.down.command.arguments` — override only the stop command arguments.
- `@docker_tasks.up.command.arguments` — override only the start command arguments.

This is how a caller can switch from the default `docker compose -f <compose-file> ...` shape to an application-local wrapper such as `bin/docker-compose`.

#### Borg task overrides

All injected Borg modules are executed with the override tag:

- `borg_tasks`

This allows ambient overrides shared across all three Borg operations, for example:

- `@borg_tasks.executable`
- `@borg_tasks.remote_repository.protocol`
- `@borg_tasks.checkpoint_interval`

Only properties that actually exist on the supplied modules participate in override resolution. In the intended usage, the supplied modules are Borg modules, so Borg-specific override keys are meaningful.

### Usage Pattern

A typical concrete job file provides three layers of data:

1. **Job identity** — `container_name`.
2. **Shared infrastructure inputs** — `docker_root`, `docker_user`, `borg_passphrase`, `backup_hosts`, `job_directory`.
3. **Operation-specific Borg module definitions** — one module context each for create, prune, and compact.

In practice:

- `borg_create` usually sets `archive_name`, `source_path`, compression, and exclusions.
- `borg_prune` usually sets `glob_archives` and retention policy.
- `borg_compact` is often empty except for optional threshold customization.

This keeps the per-job file small while still allowing full control over Borg behavior.

### Example: Basic Job

A minimal job in the style of `passbolt.jconf` usually supplies the following template-specific arguments (showing only the parts that vary per job):

```json
{
  "module": {
    "cyborg.modules.template.v1": {
      "namespace": "cyborg.template.backup-job.docker.v1",
      "path": "${cyborg_template_root}/docker-backup-template.jconf",
      "arguments": [
        { "key": "container_name", "string": "passbolt" },
        {
          "key": "borg_create",
          "cyborg.types.module.context.v1": {
            "module": {
              "cyborg.modules.borg.create.v1": {
                "archive_name": "${container_name}-{now}",
                "source_path": "${volume_root}",
                "compression": "zlib",
                "exclude": {
                  "caches": true,
                  "paths": []
                }
              }
            }
          }
        },
        {
          "key": "borg_prune",
          "cyborg.types.module.context.v1": {
            "module": {
              "cyborg.modules.borg.prune.v1": {
                "glob_archives": "${container_name}-*",
                "keep": {
                  "daily": 30,
                  "weekly": 24,
                  "monthly": 48
                }
              }
            }
          }
        },
        {
          "key": "borg_compact",
          "cyborg.types.module.context.v1": {
            "module": {
              "cyborg.modules.borg.compact.v1": { }
            }
          }
        }
      ]
    }
  }
}
```

The shared infrastructure arguments (`docker_root`, `docker_user`, `borg_passphrase`, `backup_hosts`, `job_directory`, `cyborg_template_root`) can come from a surrounding job template, environment, or configuration layer.

### Example: Custom Docker Compose Commands

A job in the style of `overleaf.jconf` can override the internal Docker task commands while still reusing the rest of the workflow (again showing only the relevant arguments):

```json
{
  "module": {
    "cyborg.modules.template.v1": {
      "namespace": "cyborg.template.backup-job.docker.v1",
      "path": "${cyborg_template_root}/docker-backup-template.jconf",
      "arguments": [
        { "key": "container_name", "string": "overleaf" },
        { "key": "@docker_tasks.command.executable", "string": "${@container_root}/bin/docker-compose" },
        { "key": "@docker_tasks.down.command.arguments", "collection<string>": [ "down" ] },
        { "key": "@docker_tasks.up.command.arguments", "collection<string>": [ "up", "-d" ] }
      ]
    }
  }
}
```

This works because the template names and groups its internal subprocess modules specifically to make those overrides addressable.

---

## Systemd Backup Template (`cyborg.template.backup-job.systemd.v1`)

### Purpose

This template implements the common workflow for backing up an application managed by `systemd`:

1. stop the service,
2. run `borg create`, `borg prune`, and `borg compact` for each configured backup host, and
3. start the service again in a `finally` block.

Like the Docker template, it treats the three Borg operations as **modules supplied as data**. Callers provide the concrete `borg_create`, `borg_prune`, and `borg_compact` module contexts, while the template injects the per-host repository and transport settings at runtime.

This is the template to use when the protected workload is controlled through `systemctl` rather than Docker Compose.

### Invocation Shape

Invoke the template through `cyborg.modules.template.v1` using the namespace declared by the template itself:

```json
{
  "cyborg.modules.template.v1": {
    "namespace": "cyborg.template.backup-job.systemd.v1",
    "path": "${cyborg_template_root}/systemd-backup-template.jconf",
    "arguments": [
      { "key": "service_name", "string": "postgresql.service" },
      { "key": "repository_name", "string": "postgresql" },
      { "key": "borg_passphrase", "string": "${some_secret}" },
      {
        "key": "backup_hosts",
        "collection<cyborg.types.borg.remote.v1>": [
          { "hostname": "nas-01", "port": 22, "borg_repo_root": "/srv/borg", "borg_user": "borg", "remote_shell": { "executable": "/usr/bin/ssh" } }
        ]
      },
      { "key": "job_directory", "string": "/etc/cyborg/jobs" },
      { "key": "borg_create", "cyborg.types.module.context.v1": { "module": { "cyborg.modules.borg.create.v1": { } } } },
      { "key": "borg_prune", "cyborg.types.module.context.v1": { "module": { "cyborg.modules.borg.prune.v1": { } } } },
      { "key": "borg_compact", "cyborg.types.module.context.v1": { "module": { "cyborg.modules.borg.compact.v1": { } } } }
    ]
  }
}
```

The declared template namespace is part of the API surface. Callers should treat `cyborg.template.backup-job.systemd.v1` as fixed for this template version.

`container_name` is **not** part of the supported argument surface of this template. Repository identity is driven by `repository_name`, and service control is driven by `service_name`.

### Required Arguments

The supported required arguments are:

| Argument | Type | Description |
|----------|------|-------------|
| `service_name` | `string` | Service unit passed to `systemctl`, for example `postgresql.service`. |
| `repository_name` | `string` | Borg repository name used for the remote repository and for the job-specific secrets file name. |
| `borg_passphrase` | `string` | Borg repository passphrase injected into the Borg tasks. |
| `backup_hosts` | `collection<cyborg.types.borg.remote.v1>` | Backup targets processed sequentially. Each host provides `hostname`, `port`, `borg_user`, `borg_repo_root`, and `remote_shell`. |
| `job_directory` | `string` | Base directory for job-specific companion files. Used to load `${job_directory}/${repository_name}.jsecrets`. |
| `borg_create` | `cyborg.types.module.context.v1` | Module context executed for archive creation. Intended to be a Borg Create module. |
| `borg_prune` | `cyborg.types.module.context.v1` | Module context executed for retention/pruning. Intended to be a Borg Prune module. |
| `borg_compact` | `cyborg.types.module.context.v1` | Module context executed for compaction. Intended to be a Borg Compact module. |

In normal usage, the caller only supplies the operation-specific properties on the Borg modules. The template injects the shared connection details per host and the repository name.

### Derived Variables

Before executing the main workflow, the template prepares only the derived runtime state it needs:

| Variable | Value | Purpose |
|----------|-------|---------|
| `host` | current element from `backup_hosts` | Per-iteration host object exposed by the `ForEach` body. |

Unlike the Docker template, this template does not derive Docker-specific path variables such as `container_root` or `volume_root`.

### Behavior

At runtime, the template behaves as follows:

1. Executes its configuration block.
   - Wires the caller-supplied module contexts into the internal dynamic modules.
   - Injects shared Borg overrides for all backup hosts.
   - Loads the job-specific secrets/configuration file.
2. Executes `systemd_tasks.stop`.
3. Enters a `Guard` block whose `finally` always executes `systemd_tasks.start`.
4. Inside the `try` block, iterates `backup_hosts` in order.
5. For each host, executes the supplied `borg_create`, `borg_prune`, and `borg_compact` module contexts in sequence.
6. If any step in the host loop fails, the loop aborts immediately and the failure propagates after service startup has been attempted in `finally`.

The template therefore guarantees service restart on the normal failure path of the backup sequence, while still surfacing the original backup failure to the caller.

### Expected Secrets File

During the configuration phase, the template loads:

```text
${job_directory}/${repository_name}.jsecrets
```

This file is loaded through `cyborg.modules.config.external.v1`, so it must contain a **configuration module**, not an arbitrary data file. In practice this usually means a `cyborg.modules.config.map.v1` or `cyborg.modules.config.collection.v1` document.

If the file is missing or invalid, template execution fails before the `systemctl stop` / `systemctl start` workflow begins.

### Injected Borg Overrides

The template applies the `borg_tasks` override tag to all three dynamically executed Borg operations and injects the following values for each current `host`:

| Override Key | Effective Value |
|--------------|-----------------|
| `@borg_tasks.remote_shell` | `${@host.remote_shell}` |
| `@borg_tasks.passphrase` | `${borg_passphrase}` |
| `@borg_tasks.remote_repository.username` | `${@host.borg_user}` |
| `@borg_tasks.remote_repository.hostname` | `${@host.hostname}` |
| `@borg_tasks.remote_repository.repository_root` | `${@host.borg_repo_root}` |
| `@borg_tasks.remote_repository.repository_name` | `${repository_name}` |
| `@borg_tasks.remote_repository.port` | `${@host.port}` |

As with the Docker template, this is the main reason the caller does **not** need to repeat host-specific repository settings in `borg_create`, `borg_prune`, and `borg_compact`.

### Override Surface Exposed by the Template

In addition to its required arguments, the template exposes a deliberate override surface through internal module names, groups, and tags.

#### Systemd task overrides

The stop/start subprocess modules use:

- group: `systemd_tasks`
- names: `systemd_tasks.stop` and `systemd_tasks.start`

This makes the following patterns possible:

- `@systemd_tasks.command.executable` — override the executable for both stop and start.
- `@systemd_tasks.stop.command.arguments` — override only the stop command arguments.
- `@systemd_tasks.start.command.arguments` — override only the start command arguments.

This is how a caller can replace the default `/usr/bin/systemctl` invocation with a wrapper or a different command layout while keeping the rest of the template unchanged.

#### Borg task overrides

All injected Borg modules are executed with the override tag:

- `borg_tasks`

This allows ambient overrides shared across all three Borg operations, for example:

- `@borg_tasks.executable`
- `@borg_tasks.remote_repository.protocol`
- `@borg_tasks.checkpoint_interval`

Only properties that actually exist on the supplied modules participate in override resolution. In the intended usage, the supplied modules are Borg modules, so Borg-specific override keys are meaningful.

### Usage Pattern

A typical concrete job file provides three layers of data:

1. **Service identity** — `service_name` and `repository_name`.
2. **Shared infrastructure inputs** — `borg_passphrase`, `backup_hosts`, `job_directory`.
3. **Operation-specific Borg module definitions** — one module context each for create, prune, and compact.

In practice:

- `borg_create` usually sets `archive_name`, `source_path`, compression, and exclusions.
- `borg_prune` usually sets `glob_archives` and retention policy.
- `borg_compact` is often empty except for optional threshold customization.

### Example: Basic Job

A minimal Systemd-backed job usually supplies the following template-specific arguments (showing only the parts that vary per job):

```json
{
  "module": {
    "cyborg.modules.template.v1": {
      "namespace": "cyborg.template.backup-job.systemd.v1",
      "path": "${cyborg_template_root}/systemd-backup-template.jconf",
      "arguments": [
        { "key": "service_name", "string": "postgresql.service" },
        { "key": "repository_name", "string": "postgresql" },
        {
          "key": "borg_create",
          "cyborg.types.module.context.v1": {
            "module": {
              "cyborg.modules.borg.create.v1": {
                "archive_name": "${repository_name}-{now}",
                "source_path": "/var/lib/postgresql",
                "compression": "lz4"
              }
            }
          }
        },
        {
          "key": "borg_prune",
          "cyborg.types.module.context.v1": {
            "module": {
              "cyborg.modules.borg.prune.v1": {
                "glob_archives": "${repository_name}-*",
                "keep": {
                  "daily": 30,
                  "weekly": 24,
                  "monthly": 48
                }
              }
            }
          }
        },
        {
          "key": "borg_compact",
          "cyborg.types.module.context.v1": {
            "module": {
              "cyborg.modules.borg.compact.v1": { }
            }
          }
        }
      ]
    }
  }
}
```

The shared infrastructure arguments (`borg_passphrase`, `backup_hosts`, `job_directory`, `cyborg_template_root`) can come from a surrounding job template, environment, or configuration layer.

### Example: Custom systemctl Commands

A caller can override the internal Systemd task commands while still reusing the rest of the workflow:

```json
{
  "module": {
    "cyborg.modules.template.v1": {
      "namespace": "cyborg.template.backup-job.systemd.v1",
      "path": "${cyborg_template_root}/systemd-backup-template.jconf",
      "arguments": [
        { "key": "service_name", "string": "postgresql.service" },
        { "key": "@systemd_tasks.command.executable", "string": "/usr/local/bin/systemctl-wrapper" },
        { "key": "@systemd_tasks.stop.command.arguments", "collection<string>": [ "stop", "${service_name}" ] },
        { "key": "@systemd_tasks.start.command.arguments", "collection<string>": [ "start", "${service_name}" ] }
      ]
    }
  }
}
```

This works because the template names and groups its internal subprocess modules specifically to make those overrides addressable.

## Compatibility Notes

- This document describes the external configuration contracts of `docker-backup-template.jconf` and `systemd-backup-template.jconf`, not built-in module IDs.
- The contracts are versioned primarily by the template namespaces: `cyborg.template.backup-job.docker.v1` and `cyborg.template.backup-job.systemd.v1`.
- Concrete jobs should treat the required arguments, derived variables, and exposed override keys in this document as the supported usage surface.
- Internal implementation details not described here should not be relied upon by callers.
