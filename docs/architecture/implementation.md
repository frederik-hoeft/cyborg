# Implementation

Implementation priorities, security principles, metrics, and extension points.

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

## Security Design Principles

### No Shell Expansion

All subprocess invocations use `Process.StartInfo.ArgumentList` (or equivalent array-based API), never string concatenation. This prevents:
- Command injection via `$()` or backticks
- Argument injection via `;`, `&&`, `||`
- Glob expansion vulnerabilities

### Input Validation

Each module validates inputs at deserialization time:
- `ServiceName` matches `^[a-zA-Z0-9_-]+\.service$`
- `MacAddress` matches MAC address format
- `Hostname` validated via DNS resolution or IP format
- File paths validated to exist before use

### Privilege Boundaries

- `run_as.user` validated against `allowed_run_as_users` list
- No arbitrary user execution
- Subprocess capabilities restricted to minimum required

### Secret Handling

- Secrets loaded into scoped environments, cleared on scope exit
- No secrets logged or exported
- Support for encrypted secret files (future: age, sops integration)

---

## Metrics Schema

All metrics use `cyborg_` prefix and follow Prometheus conventions (non-exhaustive samples below):

### Backup Metrics

```
# Archive creation
cyborg_borg_create_duration_seconds{job, repository, host}
cyborg_borg_create_success_total{job, repository, host}
cyborg_borg_create_failure_total{job, repository, host}
cyborg_borg_archive_original_size_bytes{job, repository}
cyborg_borg_archive_compressed_size_bytes{job, repository}
cyborg_borg_archive_deduplicated_size_bytes{job, repository}

# Pruning
cyborg_borg_prune_archives_kept_total{job, repository}
cyborg_borg_prune_archives_deleted_total{job, repository}

# Repository
cyborg_borg_repo_total_size_bytes{repository}
cyborg_borg_repo_total_unique_chunks{repository}
```

### Infrastructure Metrics

```
# Wake-on-LAN
cyborg_wol_wake_duration_seconds{host}
cyborg_wol_wake_total{host, result}

# Service management
cyborg_service_stop_duration_seconds{service}
cyborg_service_start_duration_seconds{service}

# Job execution
cyborg_job_duration_seconds{job, frequency}
cyborg_job_success_total{job, frequency}
cyborg_job_failure_total{job, frequency}
```

---

## Parser Grammar Requirements

Borg output parsing grammars needed to extract metrics:

### `borg create --stats` Output

```
Archive name: overleaf-2024-01-15T02:00:05
Archive fingerprint: abc123...
Time (start): Mon, 2024-01-15 02:00:05
Time (end):   Mon, 2024-01-15 02:15:32
Duration: 15 minutes 27 seconds
Number of files: 42103
Utilization of max. archive size: 0%
------------------------------------------------------------------------------
                       Original size      Compressed size    Deduplicated size
This archive:               12.45 GB              8.32 GB              1.23 GB
All archives:              156.78 GB            104.52 GB             23.45 GB
------------------------------------------------------------------------------
```

**Grammar:**
```
BorgCreateStats ::= 
  "Archive name:" ArchiveName Newline
  "Archive fingerprint:" Fingerprint Newline
  ... (timestamps)
  "Duration:" Duration Newline
  "Number of files:" FileCount Newline
  ... (separator)
  "This archive:" Size Size Size Newline
  "All archives:" Size Size Size Newline
```

### `borg prune --list` Output

```
Keeping archive: overleaf-2024-01-15T02:00:05
Pruning archive: overleaf-2024-01-01T02:00:05
```

**Grammar:**
```
BorgPruneEntry ::=
  ("Keeping" | "Pruning") "archive:" ArchiveName Newline
```

---

## Extension Points

The architecture supports future extensions:

1. **New borg commands** - Add `borg.info.v1`, `borg.list.v1`, `borg.check.v1` modules
2. **Alternative backends** - Restic support via parallel module implementations
3. **Cloud secrets** - AWS Secrets Manager, Azure Key Vault loaders
4. **Notifications** - Slack, email, webhook modules for job status
5. **Scheduling** - Potential integration with systemd timers or built-in scheduler
