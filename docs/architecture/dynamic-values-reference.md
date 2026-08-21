# Dynamic Values Reference

This document lists the dynamic value types that are part of Cyborg's current configuration surface.

Dynamic values are used in two places:

- service options loaded into the application configuration store,
- configuration modules that publish typed values into runtime environments.

For runtime semantics such as scoping, interpolation, decomposition, and override resolution, see [Cyborg Architecture](../architecture.md).

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Dynamic Value Format](#dynamic-value-format)
  - [Entry Shape](#entry-shape)
  - [Type Name Syntax](#type-name-syntax)
- [Scalar Types](#scalar-types)
  - [Tagged strings and secrets](#tagged-strings-and-secrets)
  - [Configuration Enum Types](#configuration-enum-types)
- [Runtime Composition Types](#runtime-composition-types)
  - [`cyborg.types.module.reference.v1`](#cyborgtypesmodulereferencev1)
  - [`cyborg.types.module.environment.v1`](#cyborgtypesmoduleenvironmentv1)
  - [`cyborg.types.module.context.v1`](#cyborgtypesmodulecontextv1)
- [Borg Types](#borg-types)
  - [`cyborg.types.borg.remote.v1.4`](#cyborgtypesborgremotev14)
  - [`cyborg.types.borg.repository.v1.4`](#cyborgtypesborgrepositoryv14)
  - [`cyborg.types.borg.exclude.v1.4`](#cyborgtypesborgexcludev14)
  - [`cyborg.types.borg.files_cache_sentinel.v1.4`](#cyborgtypesborgfiles_cache_sentinelv14)
- [Service Option Types](#service-option-types)
  - [`cyborg.types.services.trust.options.v1`](#cyborgtypesservicestrustoptionsv1)
  - [`cyborg.trust.policy.unix.owner`](#cyborgtrustpolicyunixowner)
  - [`cyborg.trust.policy.unix.permissions`](#cyborgtrustpolicyunixpermissions)
  - [`cyborg.types.services.logging.v1`](#cyborgtypesservicesloggingv1)
  - [`cyborg.types.services.logging.console.v1`](#cyborgtypesservicesloggingconsolev1)
  - [`cyborg.types.services.logging.file.v1`](#cyborgtypesservicesloggingfilev1)
  - [`cyborg.types.services.logging.rolling.v1`](#cyborgtypesservicesloggingrollingv1)
  - [`cyborg.types.services.metrics.v1`](#cyborgtypesservicesmetricsv1)
- [Generic Types](#generic-types)
  - [`collection<T>`](#collectiont)
- [Notes](#notes)

<!-- /code_chunk_output -->

---

## Dynamic Value Format

### Entry Shape

A dynamic value entry is an object containing:

- `key`: the variable or option name,
- exactly one additional property whose name is the dynamic type name.

Example:

```json
{
  "key": "target",
  "string": "daily"
}
```

The converter accepts the key and typed-value properties in either order, but both must be present and non-null. A missing key, missing value, second typed-value property, or malformed typed value causes deserialization to fail with `JsonException`. These structural checks occur in the converter because dynamic key-value pairs are also used by configuration parsing paths that do not run module validation. Semantic checks such as an empty or whitespace key are handled later by `[Required]` when the containing model participates in generated validation.

### Type Name Syntax

Type names are either:

- simple names such as `string` or `cyborg.types.module.context.v1`, or
- generic names such as `collection<string>` or `collection<cyborg.types.borg.remote.v1.4>`.

Generic type names are parsed recursively, so nested generic composition is allowed.

## Scalar Types

The following scalar types are registered by the core runtime:

| Type name | JSON value kind |
|-----------|-----------------|
| `string` | string |
| `cyborg.types.taggedstring.v1` | string, or `{ "value": string, "tags": [string] }` |
| `cyborg.types.secret.v1` | string, or `{ "value": string, "tags": [string] }` (always includes `cyborg.secret.v1`) |
| `bool` | boolean |
| `sbyte` | number |
| `byte` | number |
| `short` | number |
| `ushort` | number |
| `int` | number |
| `uint` | number |
| `long` | number |
| `ulong` | number |
| `float` | number |
| `double` | number |
| `decimal` | number |

### Tagged strings and secrets

`cyborg.types.taggedstring.v1` stores a `TaggedString`. A JSON string is an untagged value. A structural object supplies the raw `value` plus zero or more tag names.

`cyborg.types.secret.v1` is a convenience form for configuration maps and other dynamic entries that should be treated as secrets by Cyborg-controlled presentation paths. The resulting `TaggedString` always carries `cyborg.secret.v1`, in addition to any tags supplied structurally.

```json
{ "key": "borg_passphrase", "cyborg.types.secret.v1": "your-passphrase" }
```

```json
{
  "key": "token",
  "cyborg.types.taggedstring.v1": {
    "value": "payload",
    "tags": ["cyborg.secret.v1", "application.custom.v1"]
  }
}
```

DI-aware Cyborg presentation surfaces render secret-tagged values through `ITaggedStringRenderer`; the built-in secret policy produces `[REDACTED]`. Interpolation and exact-reference resolution preserve or union tags, so a secret introduced through a dynamic value remains secret when composed into another tagged value. The raw string remains available through `TaggedString.Value` for subprocess execution and other explicit execution boundaries. Converting to raw `string` intentionally leaves the tagged-value model, so arbitrary code operating on that string is responsible for how it is subsequently exposed.

### Configuration Enum Types

Configuration-facing enum leaves have dedicated dynamic value providers so command-line and other dynamic sources can preserve the leaf's enum type instead of storing a string. Their values are JSON strings using the same snake_case enum naming convention as configuration files.

| Type name | Enum |
|-----------|------|
| `cyborg.types.services.logging.level.v1` | `LogLevel` |
| `cyborg.types.services.logging.format.v1` | `LogFormat` |
| `cyborg.types.services.logging.rolling_interval.v1` | `RollingInterval` |
| `cyborg.types.services.trust.enforcement_mode.v1` | `TrustEnforcementMode` |

For example, `"log_only"` is a valid value for `cyborg.types.services.trust.enforcement_mode.v1`. These providers are registered by the service layer that owns the corresponding option type rather than by the core primitive-provider set.

## Runtime Composition Types

### `cyborg.types.module.reference.v1`

Represents a module definition as data.

The value is a normal module-discriminator object such as:

```json
{
  "key": "borg_create",
  "cyborg.types.module.reference.v1": {
    "cyborg.modules.borg.create.v1.4": {
      "archive_name": "${container_name}-{now}",
      "source_path": "${volume_root}"
    }
  }
}
```

Use this type when a value should hold a module worker that will be executed later through another module such as `dynamic`.

### `cyborg.types.module.environment.v1`

Represents environment scoping configuration.

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `scope` | enum | No | `inherit_parent` |
| `name` | string | No | `null` |
| `transient` | bool | No | `false` |

Example:

```json
{
  "key": "cleanup_environment",
  "cyborg.types.module.environment.v1": {
    "scope": "reference",
    "name": "jobs"
  }
}
```

### `cyborg.types.module.context.v1`

Represents a full executable `ModuleContext`.

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `module` | module reference | Yes | - |
| `environment` | module environment | No | `inherit_parent` |
| `configuration` | module reference | No | `null` |
| `requires` | requirements object | No | no argument requirements |

This is the main type used by template arguments that supply executable workflows as data.

## Borg Types

### `cyborg.types.borg.remote.v1.4`

Represents a backup host entry.

| Property | Type | Required |
|----------|------|----------|
| `hostname` | string | Yes |
| `port` | int | Yes |
| `wake_on_lan_mac` | string | No |
| `borg_repo_root` | string | Yes |
| `borg_user` | string | Yes |
| `remote_shell` | Borg SSH options | Yes |

`remote_shell` can contain:

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `executable` | string | No | `/usr/bin/ssh` |
| `ssh_pass` | object | No | `null` |

`ssh_pass` can contain:

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `executable` | string | No | `/usr/bin/sshpass` |
| `file_path` | string | Yes | - |
| `match_prompt` | string | No | `null` |

### `cyborg.types.borg.repository.v1.4`

Represents a fully qualified Borg repository target.

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `protocol` | string | No | `ssh://` |
| `username` | string | Yes | - |
| `hostname` | string | Yes | - |
| `port` | int | Yes | - |
| `repository_root` | string | No | `null` |
| `repository_name` | string | Yes | - |

This is usually injected through overrides inside the reusable backup templates rather than authored directly in every job.

### `cyborg.types.borg.exclude.v1.4`

Represents exclusion options for a Borg create operation.

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `caches` | bool | No | `false` |
| `paths` | array of strings | No | `[]` |

### `cyborg.types.borg.files_cache_sentinel.v1.4`

Represents files-cache sentinel options for a Borg create operation. See [Borg Create](./modules-reference.md#borg-create-cyborgmodulesborgcreatev14) for a description of the sentinel mechanism.

| Property | Type | Required | Default | Constraints |
|----------|------|----------|---------|-------------|
| `enabled` | bool | No | `false` | -- |
| `archive_path` | string | No | `".cyborg"` | Unrooted, normalized path |
| `sentinel_file_name` | string | No | `"borg_files_cache.sentinel"` | Valid file name |

## Service Option Types

Service option providers deserialize convenient structured source values. When these values are added to host configuration, `IConfiguration` recursively decomposes them and retains only their dot-addressed leaf values; the structured provider types are not stored as retrievable parent nodes.

### `cyborg.types.services.trust.options.v1`

Represents the root trust-service options object.

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `policies` | array of dynamic policy values | No | empty |
| `enforcement_mode` | `enforce`, `log_only`, or `disabled` | No | `enforce` |

### `cyborg.trust.policy.unix.owner`

Represents the Unix owner/group trust policy.

| Property | Type | Required |
|----------|------|----------|
| `allowed_users` | array of strings | No |
| `allowed_groups` | array of strings | No |

### `cyborg.trust.policy.unix.permissions`

Represents the Unix file-mode trust policy.

| Property | Type | Required |
|----------|------|----------|
| `required_bits` | array of Unix file-mode enum names | No |
| `forbidden_bits` | array of Unix file-mode enum names | No |

### `cyborg.types.services.logging.v1`

Represents global logging defaults.

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `minimum_level` | `LogLevel` enum name | No | `information` |

### `cyborg.types.services.logging.console.v1`

Represents console logging configuration.

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `enabled` | bool | No | `true` |
| `minimum_level` | `LogLevel` enum name | No | `information` |
| `format` | `json` or `text` | No | `text` |

### `cyborg.types.services.logging.file.v1`

Represents single-file logging configuration.

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `enabled` | bool | No | `true` |
| `minimum_level` | `LogLevel` enum name | No | `information` |
| `path` | string | No | `/var/log/cyborg/latest.log` |
| `format` | `json` or `text` | No | `json` |

### `cyborg.types.services.logging.rolling.v1`

Represents rolling file logging configuration.

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `enabled` | bool | No | `true` |
| `minimum_level` | `LogLevel` enum name | No | `information` |
| `path` | string | No | `/var/log/cyborg` |
| `rolling_interval` | `RollingInterval` enum name | No | `day` |
| `rolling_size_bytes` | int | No | `10485760` |
| `format` | `json` or `text` | No | `json` |

### `cyborg.types.services.metrics.v1`

Represents metrics output configuration.

| Property | Type | Required | Default |
|----------|------|----------|---------|
| `namespace` | string | No | `cyborg` |
| `file_path` | string | No | `/var/log/cyborg/metrics.prom` |

## Generic Types

### `collection<T>`

Represents a typed read-only collection. `T` can be any registered dynamic value type, including another generic type.

Common examples:

- `collection<string>`
- `collection<int>`
- `collection<cyborg.types.borg.remote.v1.4>`
- `collection<cyborg.types.module.context.v1>`

## Notes

- The public dynamic type surface is defined by the providers registered in the service provider modules.
- A type helper existing in source code is not part of the supported configuration contract unless its provider is registered.
- Per-module nested records such as `SubprocessCommand` are regular JSON object shapes inside their parent modules, not standalone dynamic value types.
