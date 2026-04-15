# Templates Reference

This document describes the reusable configuration templates currently shipped in `samples/templates/`.

These templates are not built-in modules. They are external `ModuleContext` documents intended to be invoked through [`cyborg.modules.template.v1`](modules-reference.md#template-cyborgmodulestemplatev1).

For runtime behavior such as environment scoping, overrides, artifacts, and variable resolution, see [Cyborg Architecture](../architecture.md). For the typed argument payloads used by the templates, see [Dynamic Values Reference](dynamic-values-reference.md).

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Template Conventions](#template-conventions)
- [Docker Backup Template (`cyborg.template.backup-job.docker.v1`)](#docker-backup-template-cyborgtemplatebackup-jobdockerv1)
  - [Purpose](#purpose)
  - [Required Arguments](#required-arguments)
  - [Derived Variables](#derived-variables)
  - [Injected Override Surface](#injected-override-surface)
  - [Exposed Internal Override Targets](#exposed-internal-override-targets)
- [Systemd Backup Template (`cyborg.template.backup-job.systemd.v1`)](#systemd-backup-template-cyborgtemplatebackup-jobsystemdv1)
  - [Purpose](#purpose-1)
  - [Required Arguments](#required-arguments-1)
  - [Derived Variables](#derived-variables-1)
  - [Injected Override Surface](#injected-override-surface-1)
  - [Exposed Internal Override Targets](#exposed-internal-override-targets-1)
- [Systemd Service Wrapper Template (`cyborg.template.systemd.v1`)](#systemd-service-wrapper-template-cyborgtemplatesystemdv1)
  - [Purpose](#purpose-2)
  - [Required Arguments](#required-arguments-2)
  - [Derived Variables](#derived-variables-2)
  - [Injected Override Surface](#injected-override-surface-2)
  - [Exposed Internal Override Targets](#exposed-internal-override-targets-2)
- [Compatibility Notes](#compatibility-notes)

<!-- /code_chunk_output -->

---

## Template Conventions

All shipped templates follow the same basic conventions.

- They are invoked through `cyborg.modules.template.v1`.
- The template namespace is part of the contract and should be treated as versioned API surface.
- Arguments are published into the caller environment under the declared template namespace and then consumed by the template's `requires` contract.
- Template-local overrides are passed through the `TemplateModule.Overrides` collection.
- Templates frequently treat modules as data by accepting `cyborg.types.module.context.v1` arguments and executing them later through `cyborg.modules.dynamic.v1`.

In practice, that means a template controls the workflow skeleton while the caller supplies the job-specific payload.

## Docker Backup Template (`cyborg.template.backup-job.docker.v1`)

### Purpose

This template stops a Dockerized workload, runs Borg create/prune/compact for each configured backup host, and starts the workload again in a `finally` block.

The workflow shape is:

1. derive Docker path variables,
2. load the job-specific secrets/configuration file,
3. stop the container group,
4. iterate backup hosts,
5. execute the supplied Borg module contexts through `dynamic`,
6. start the container group again regardless of failure in the guarded body.

### Required Arguments

| Argument | Type | Purpose |
|----------|------|---------|
| `container_name` | `string` | Logical workload name and Borg repository name |
| `docker_root` | `string` | Base path for the expected Docker layout |
| `docker_user` | `string` | User used for Docker subprocess impersonation |
| `backup_hosts` | `collection<cyborg.types.borg.remote.v1.4>` | Host list iterated by the template |
| `job_directory` | `string` | Base directory for `${container_name}.jsecrets` |
| `borg_create` | `cyborg.types.module.context.v1` | Create operation supplied as data |
| `borg_prune` | `cyborg.types.module.context.v1` | Prune operation supplied as data |
| `borg_compact` | `cyborg.types.module.context.v1` | Compact operation supplied as data |

In normal usage, `borg_passphrase` is also required at execution time. The template checks for it through an inner `requires` block before it enters the guarded Borg workflow.

### Derived Variables

| Variable | Value |
|----------|-------|
| `container_root` | `${docker_root}/containers/${container_name}` |
| `volume_root` | `${docker_root}/volumes/${container_name}` |
| `host` | Current element from `backup_hosts` during `foreach` |

`container_root` and `volume_root` are intentionally part of the practical contract because caller-supplied Borg create modules usually reference them.

### Injected Override Surface

For each current `host`, the template injects ambient Borg overrides under the `borg_tasks` tag.

| Override key | Effective value |
|--------------|-----------------|
| `@borg_tasks.remote_shell` | `${@host.remote_shell}` |
| `@borg_tasks.passphrase` | `${@borg_passphrase}` |
| `@borg_tasks.remote_repository` | Borg repository object built from the current host and `container_name` |
| `@borg_tasks.remote_repository.port` | `${@host.port}` |
| `@borg_create.target` | `${borg_create}` |
| `@borg_prune.target` | `${borg_prune}` |
| `@borg_compact.target` | `${borg_compact}` |

The Borg operation contexts are therefore expected to define what to do, while the template injects where to do it for the current host.

### Exposed Internal Override Targets

The template deliberately exposes a small override surface on its internal subprocess modules:

- group: `docker_tasks`
- names: `docker_tasks.down` and `docker_tasks.up`

Common override examples are:

- `@docker_tasks.command.executable`
- `@docker_tasks.impersonation.user`
- `@docker_tasks.down.command.arguments`
- `@docker_tasks.up.command.arguments`

## Systemd Backup Template (`cyborg.template.backup-job.systemd.v1`)

### Purpose

This template is the systemd-oriented counterpart to the Docker backup template. It stops a service, runs Borg create/prune/compact for each backup host, and starts the service again in a `finally` block.

### Required Arguments

| Argument | Type | Purpose |
|----------|------|---------|
| `service_name` | `string` | Service name passed to `systemctl` |
| `repository_name` | `string` | Borg repository name and secrets-file stem |
| `backup_hosts` | `collection<cyborg.types.borg.remote.v1.4>` | Host list iterated by the template |
| `job_directory` | `string` | Base directory for `${repository_name}.jsecrets` |
| `borg_create` | `cyborg.types.module.context.v1` | Create operation supplied as data |
| `borg_prune` | `cyborg.types.module.context.v1` | Prune operation supplied as data |
| `borg_compact` | `cyborg.types.module.context.v1` | Compact operation supplied as data |

As with the Docker template, `borg_passphrase` is required indirectly through an inner `requires` check before the guarded Borg workflow runs.

### Derived Variables

| Variable | Value |
|----------|-------|
| `host` | Current element from `backup_hosts` during `foreach` |

This template does not derive Docker-specific path variables.

### Injected Override Surface

| Override key | Effective value |
|--------------|-----------------|
| `@borg_tasks.remote_shell` | `${@host.remote_shell}` |
| `@borg_tasks.passphrase` | `${@borg_passphrase}` |
| `@borg_tasks.remote_repository` | Borg repository object built from the current host and `repository_name` |
| `@borg_tasks.remote_repository.port` | `${@host.port}` |
| `@borg_create.target` | `${borg_create}` |
| `@borg_prune.target` | `${borg_prune}` |
| `@borg_compact.target` | `${borg_compact}` |

### Exposed Internal Override Targets

- group: `systemd_tasks`
- names: `systemd_tasks.stop` and `systemd_tasks.start`

Common override examples are:

- `@systemd_tasks.command.executable`
- `@systemd_tasks.stop.command.arguments`
- `@systemd_tasks.start.command.arguments`

## Systemd Service Wrapper Template (`cyborg.template.systemd.v1`)

### Purpose

This template is a simpler service wrapper. It stops a service, executes one caller-supplied body task once per backup host, and starts the service again in a `finally` block.

Unlike the backup-specific templates, it does not know anything about Borg. It is a generic service-stop / iterate-hosts / service-start workflow skeleton.

### Required Arguments

| Argument | Type | Purpose |
|----------|------|---------|
| `service_name` | `string` | Service name passed to `systemctl` |
| `backup_hosts` | `collection<cyborg.types.borg.remote.v1.4>` | Host list iterated by the template |
| `body_task` | `cyborg.types.module.context.v1` | Task executed once per host through `dynamic` |

### Derived Variables

| Variable | Value |
|----------|-------|
| `host` | Current element from `backup_hosts` during `foreach` |

### Injected Override Surface

| Override key | Effective value |
|--------------|-----------------|
| `@body_task.target` | `${body_task}` |

### Exposed Internal Override Targets

- group: `systemd_tasks`
- names: `systemd_tasks.stop` and `systemd_tasks.start`

Common override examples are:

- `@systemd_tasks.command.executable`
- `@systemd_tasks.stop.command.arguments`
- `@systemd_tasks.start.command.arguments`

## Compatibility Notes

- The supported contract of a template is the combination of its namespace, required arguments, derived variables, and intentionally exposed override targets.
- Internal module layout not described here should not be treated as stable API surface.
- These templates are sample-shipped workflow assets. They evolve like versioned configuration contracts rather than like built-in CLR types.
