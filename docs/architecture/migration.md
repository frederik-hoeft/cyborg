# Migration Design

This document describes the design for migrating the legacy bash-based backup scripts (`/borg/`) to the declarative .NET Cyborg framework.

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

## Migration Goals

1. **Functional parity** - Replicate all existing backup workflows (daily/weekly/monthly jobs)
2. **Declarative configuration** - JSON-driven backup definitions with no embedded shell scripts
3. **Injection-safe** - No raw command strings; all arguments are typed records validated at deserialization
4. **Composable modules** - Granular modules that compose into complex workflows
5. **Observable** - Prometheus metrics for backup health, duration, and borg statistics
6. **AOT-compatible** - All modules must work with native AOT compilation

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

## Module Hierarchy

Legend: ✅ Implemented | 🚧 Partial/WIP | ❌ Planned

```
Cyborg.Modules/
├── Borg/                    # Borg backup operations
│   └── BorgJobModule        # 🚧 cyborg.modules.borg.job.v1 (wrapper, not registered)
│   # Planned:
│   # ├── Create/            # ❌ cyborg.modules.borg.create.v1
│   # ├── Prune/             # ❌ cyborg.modules.borg.prune.v1
│   # ├── Compact/           # ❌ cyborg.modules.borg.compact.v1
│   # └── Repository/        # ❌ cyborg.modules.borg.repository.v1
├── Conditional/             # ✅ cyborg.modules.if.v1
├── Configuration/           # Configuration modules
│   ├── ConfigMap/           # ✅ cyborg.modules.config.map.v1
│   └── ConfigCollection/    # ✅ cyborg.modules.config.collection.v1
├── Foreach/                 # ✅ cyborg.modules.foreach.v1
├── Named/                   # Named module definitions
│   ├── Definition/          # ✅ cyborg.modules.named.definition.v1
│   └── Reference/           # ✅ cyborg.modules.named.reference.v1
├── Network/                 # Network operations
│   ├── WakeOnLan/           # ✅ cyborg.modules.network.wol.v1
│   └── SshShutdown/         # 🚧 cyborg.modules.network.ssh_shutdown.v1 (module only)
├── Sequence/                # ✅ cyborg.modules.sequence.v1
├── Subprocess/              # ✅ cyborg.modules.subprocess.v1
├── Template/                # ✅ cyborg.modules.template.v1
# Planned:
# ├── Guard/                 # ❌ cyborg.modules.guard.v1 (try-finally)
# ├── Services/              # Service lifecycle management
# │   ├── Docker/            # ❌ cyborg.modules.docker.{up,down}.v1
# │   └── Systemd/           # ❌ cyborg.modules.systemd.{start,stop}.v1
# ├── Security/              # Secrets management
# │   └── Secrets/           # ❌ cyborg.modules.secrets.load.v1
# └── System/                # System operations
#     └── RunAs/             # ❌ cyborg.modules.system.run_as.v1
```
