# Dynamic Values Reference

This document covers all dynamic value types currently supported by Cyborg configuration deserialization.

Dynamic values are primarily used by configuration modules (for example, ConfigMap and ConfigCollection) to publish typed values into the runtime environment. Each dynamic value entry has a `key` and exactly one typed value property.

For runtime behavior (resolution, scoping, interpolation, decomposition), see [Runtime Infrastructure](runtime.md).

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Dynamic Value Format](#dynamic-value-format)
  - [Key-Value Entry Shape](#key-value-entry-shape)
  - [Type Name Syntax](#type-name-syntax)
- [Supported Scalar Types](#supported-scalar-types)
- [Supported Structural Types](#supported-structural-types)
  - [Module Reference (`cyborg.types.module.reference.v1`)](#module-reference-cyborgtypesmodulereferencev1)
  - [Module Environment (`cyborg.types.module.environment.v1`)](#module-environment-cyborgtypesmoduleenvironmentv1)
  - [Module Context (`cyborg.types.module.context.v1`)](#module-context-cyborgtypesmodulecontextv1)
- [Supported Borg Types](#supported-borg-types)
  - [Borg Remote (`cyborg.types.borg.remote.v1.4`)](#borg-remote-cyborgtypesborgremotev14)
  - [Borg Repository (`cyborg.types.borg.repository.v1.4`)](#borg-repository-cyborgtypesborgrepositoryv14)
- [Generic Collection Type](#generic-collection-type)
  - [Collection (`collection<T>`)](#collection-collectiont)
- [Usage Patterns](#usage-patterns)
  - [Typed Scalar Variable](#typed-scalar-variable)
  - [Late-Bound Module Injection](#late-bound-module-injection)
  - [Collection of Structured Values](#collection-of-structured-values)
- [Compatibility Note](#compatibility-note)

<!-- /code_chunk_output -->

---

## Dynamic Value Format

### Key-Value Entry Shape

A dynamic entry is an object with:

- `key`: the environment variable name to publish.
- one additional property: the dynamic type name.

Example:

```json
{
  "key": "max_retries",
  "int": 3
}
```

Only one typed value property is allowed per entry.

### Type Name Syntax

Dynamic type names support:

- simple names (for example, `string`, `int`, `cyborg.types.module.context.v1`)
- generic names with type arguments (for example, `collection<string>`, `collection<cyborg.types.borg.remote.v1.4>`)
- nested generic composition (for example, `collection<collection<int>>`)

---

## Supported Scalar Types

The following scalar type names are currently supported:

| Type Name | JSON Value Kind | Notes |
|----------|------------------|-------|
| `string` | string | Text value |
| `bool` | boolean | `true` / `false` |
| `sbyte` | number | Signed 8-bit integer |
| `byte` | number | Unsigned 8-bit integer |
| `short` | number | Signed 16-bit integer |
| `ushort` | number | Unsigned 16-bit integer |
| `int` | number | Signed 32-bit integer |
| `uint` | number | Unsigned 32-bit integer |
| `long` | number | Signed 64-bit integer |
| `ulong` | number | Unsigned 64-bit integer |
| `float` | number | 32-bit floating point |
| `double` | number | 64-bit floating point |
| `decimal` | number | High-precision decimal |

---

## Supported Structural Types

### Module Reference (`cyborg.types.module.reference.v1`)

Represents a module reference value. Use this when a variable should hold a full module definition that can be executed later (for example, by the Dynamic module).

**Value shape:** module-discriminator object.

Example:

```json
{
  "key": "selected_step",
  "cyborg.types.module.reference.v1": {
    "cyborg.modules.subprocess.v1": {
      "command": {
        "executable": "/usr/bin/echo",
        "arguments": ["hello"]
      },
      "output": {
        "read_stdout": true,
        "read_stderr": false
      },
      "check_exit_code": true
    }
  }
}
```

---

### Module Environment (`cyborg.types.module.environment.v1`)

Represents environment scoping settings for module execution.

**Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `scope` | enum | No | `inherit_parent` | Environment scope strategy |
| `name` | string | No | `null` | Optional scope name |
| `transient` | bool | No | `false` | If true, environment is not globally registered |

Example:

```json
{
  "key": "target_environment",
  "cyborg.types.module.environment.v1": {
    "scope": "reference",
    "name": "backup_session",
    "transient": false
  }
}
```

---

### Module Context (`cyborg.types.module.context.v1`)

Represents a full executable context (`module` + `environment` + optional `configuration` + template metadata).

**Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `module` | module reference | Yes | -- | Main module to execute |
| `environment` | module environment | No | `{ "scope": "inherit_parent" }` | Scope for this execution |
| `configuration` | module reference | No | `null` | Optional pre-configuration module |
| `template` | object | No | `{ "namespace": null, "arguments": [] }` | Template metadata |

Example:

```json
{
  "key": "fallback_step",
  "cyborg.types.module.context.v1": {
    "module": {
      "cyborg.modules.subprocess.v1": {
        "command": {
          "executable": "/usr/bin/echo",
          "arguments": ["fallback"]
        },
        "output": {
          "read_stdout": true,
          "read_stderr": false
        },
        "check_exit_code": true
      }
    },
    "environment": {
      "scope": "inherit_parent"
    },
    "template": {
      "namespace": null,
      "arguments": []
    }
  }
}
```

---

## Supported Borg Types

### Borg Remote (`cyborg.types.borg.remote.v1.4`)

Represents a remote host target used in Borg workflows.

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `hostname` | string | Yes | Remote host name |
| `port` | int | Yes | SSH port |
| `wake_on_lan_mac` | string | No | Optional WoL MAC address |
| `borg_repo_root` | string | Yes | Root path for repositories on remote |
| `borg_user` | string | Yes | Remote user for Borg operations |
| `remote_shell` | object | Yes | SSH execution options |

`remote_shell` supports:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `executable` | string | No | `/usr/bin/ssh` | SSH client executable |
| `ssh_pass` | object | No | `null` | Optional sshpass settings |

`ssh_pass` supports:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `executable` | string | No | `/usr/bin/sshpass` | sshpass executable |
| `file_path` | string | Yes | -- | Password file path |
| `match_prompt` | string | No | `null` | Optional prompt matcher |

Example:

```json
{
  "key": "backup_target",
  "cyborg.types.borg.remote.v1.4": {
    "hostname": "nas-01",
    "port": 22,
    "wake_on_lan_mac": "00:11:22:33:44:55",
    "borg_repo_root": "/srv/borg",
    "borg_user": "borg",
    "remote_shell": {
      "executable": "/usr/bin/ssh",
      "ssh_pass": {
        "executable": "/usr/bin/sshpass",
        "file_path": "/run/secrets/ssh_password",
        "match_prompt": null
      }
    }
  }
}
```

---

### Borg Repository (`cyborg.types.borg.repository.v1.4`)

Represents a concrete repository location tuple used by Borg modules.

**Properties:**

| Property | Type | Required | Default | Constraints | Description |
|----------|------|----------|---------|-------------|-------------|
| `protocol` | string | No | `ssh://` | -- | URI protocol prefix |
| `username` | string | Yes | -- | -- | Repository user |
| `hostname` | string | Yes | -- | -- | Repository host |
| `port` | int | Yes | -- | `1` to `65535` | Repository port |
| `repository_root` | string | No | `null` | -- | Optional base directory |
| `repository_name` | string | Yes | -- | -- | Repository name |

Example:

```json
{
  "key": "primary_repo",
  "cyborg.types.borg.repository.v1.4": {
    "protocol": "ssh://",
    "username": "borg",
    "hostname": "nas-01",
    "port": 22,
    "repository_root": "/srv/borg",
    "repository_name": "daily"
  }
}
```

---

## Generic Collection Type

### Collection (`collection<T>`)

Represents a typed list. `T` can be any supported dynamic type, including structured or generic types.

Example scalar collection:

```json
{
  "key": "ports",
  "collection<int>": [22, 443, 8080]
}
```

Example structured collection:

```json
{
  "key": "backup_hosts",
  "collection<cyborg.types.borg.remote.v1.4>": [
    {
      "hostname": "nas-01",
      "port": 22,
      "wake_on_lan_mac": null,
      "borg_repo_root": "/srv/borg",
      "borg_user": "borg",
      "remote_shell": {
        "executable": "/usr/bin/ssh",
        "ssh_pass": null
      }
    },
    {
      "hostname": "nas-02",
      "port": 22,
      "wake_on_lan_mac": null,
      "borg_repo_root": "/srv/borg",
      "borg_user": "borg",
      "remote_shell": {
        "executable": "/usr/bin/ssh",
        "ssh_pass": null
      }
    }
  ]
}
```

---

## Usage Patterns

### Typed Scalar Variable

```json
{
  "key": "max_parallel_jobs",
  "int": 4
}
```

### Late-Bound Module Injection

```json
{
  "key": "dynamic_target",
  "cyborg.types.module.context.v1": {
    "module": {
      "cyborg.modules.named.ref.v1": {
        "name": "nightly_backup"
      }
    },
    "environment": {
      "scope": "inherit_parent"
    },
    "template": {
      "namespace": null,
      "arguments": []
    }
  }
}
```

### Collection of Structured Values

```json
{
  "key": "repositories",
  "collection<cyborg.types.borg.repository.v1.4>": [
    {
      "protocol": "ssh://",
      "username": "borg",
      "hostname": "nas-01",
      "port": 22,
      "repository_root": "/srv/borg",
      "repository_name": "daily"
    }
  ]
}
```

---

## Compatibility Note

This reference lists types currently supported by the active dynamic value provider registration in the runtime service composition.

Provider classes that exist in source but are not registered are intentionally not listed as supported here.
