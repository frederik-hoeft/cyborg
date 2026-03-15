# Configuration Examples

Environment scoping patterns and complete job configuration examples.

<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

## Environment Scoping Patterns for Backup Workflows

This section describes common patterns for leveraging the environment scoping system in backup job configurations.

### Pattern 1: Named Scope with Guard Cleanup

The guard module's `finally` block needs access to variables set during the `body`. Use named environments with `reference` scope:

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
            { "key": "compose_path", "string": "/opt/docker/containers/app/docker-compose.yml" }
          ]
        }
      },
      "module": { ... }
    },
    "finally": {
      "environment": {
        "scope": "reference",
        "name": "backup_session"
      },
      "module": {
        "cyborg.modules.docker.up.v1": {
          "compose_path": "${compose_path}"
        }
      }
    }
  }
}
```

### Pattern 2: ForEach Iteration Variables

ForEach sets `item_variable` in a child scope that inherits from parent. Child modules can read both iteration variables and parent variables:

```json
{
  "cyborg.modules.foreach.v1": {
    "collection": "backup_hosts",
    "item_variable": "host",
    "body": {
      "environment": {
        "scope": "inherit_parent"
      },
      "module": {
        "cyborg.modules.borg.repository.v1": {
          "hostname": "${host.hostname}",
          "repository_name": "${container_name}"
        }
      }
    }
  }
}
```

### Pattern 3: Secrets via Configuration Block

Load secrets before the main module executes using the `configuration` property:

```json
{
  "environment": {
    "scope": "inherit_parent",
    "name": "job_scope"
  },
  "configuration": {
    "cyborg.modules.config.collection.v1": {
      "sources": [
        {
          "cyborg.modules.secrets.load.v1": {
            "source": "backup-secrets",
            "source_type": "systemd_credential",
            "environment_prefix": "secrets"
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
  "module": {
    "cyborg.modules.borg.repository.v1": {
      "passphrase_variable": "secrets.passphrase"
    }
  }
}
```

### Pattern 4: Scope Isolation for Nested Loops

When nesting foreach loops, use `inherit_parent` to create a scope chain:

```json
{
  "cyborg.modules.foreach.v1": {
    "collection": "smb_targets",
    "item_variable": "target",
    "body": {
      "environment": { "scope": "inherit_parent" },
      "module": {
        "cyborg.modules.foreach.v1": {
          "collection": "backup_hosts",
          "item_variable": "host",
          "body": {
            "environment": { "scope": "inherit_parent" },
            "module": {
              "cyborg.modules.borg.create.v1": {
                "archive_name_pattern": "${target.name}-{now}",
                "paths": [ "${target.root}" ]
              }
            }
          }
        }
      }
    }
  }
}
```

Variable resolution chain: `inner iteration → outer iteration → parent → global`

### Pattern 5: Parent Scope for In-Place Mutation

Use `scope: parent` when a module should modify the calling scope directly:

```json
{
  "cyborg.modules.sequence.v1": {
    "steps": [
      {
        "environment": { "scope": "parent" },
        "module": {
          "cyborg.modules.config.map.v1": {
            "entries": [
              { "key": "step1_result", "string": "computed_value" }
            ]
          }
        }
      },
      {
        "environment": { "scope": "parent" },
        "module": {
          "cyborg.modules.log.v1": {
            "message": "Step 1 set: ${step1_result}"
          }
        }
      }
    ]
  }
}
```

---

## Complete Job Configuration Examples

### Example: Overleaf Daily Backup (`jobs/daily/overleaf.json`)

This example demonstrates:
- Named guard scope for cleanup variable access
- Configuration block for secrets + config loading
- ForEach iteration with inherited scopes
- Repository module setting borg environment variables

```json
{
  "environment": {
    "scope": "inherit_parent",
    "name": "overleaf_job"
  },
  "configuration": {
    "cyborg.modules.config.collection.v1": {
      "sources": [
        {
          "cyborg.modules.secrets.load.v1": {
            "source": "overleaf-secrets",
            "source_type": "systemd_credential",
            "environment_prefix": "secrets"
          }
        },
        {
          "cyborg.modules.config.map.v1": {
            "entries": [
              { "key": "container_name", "string": "overleaf" },
              { "key": "container_root", "string": "/opt/docker/containers/overleaf" },
              { "key": "volume_root", "string": "/opt/docker/volumes/overleaf" },
              { "key": "docker_user", "string": "docker" }
            ]
          }
        }
      ]
    }
  },
  "module": {
    "cyborg.modules.guard.v1": {
      "body": {
        "environment": { "scope": "parent" },
        "module": {
          "cyborg.modules.sequence.v1": {
            "steps": [
              {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.docker.down.v1": {
                    "compose_path": "${container_root}/docker-compose.yml",
                    "user": "${docker_user}"
                  }
                }
              },
              {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.log.v1": {
                    "level": "info",
                    "message": "Starting backup for ${container_name}"
                  }
                }
              },
              {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.foreach.v1": {
                    "collection": "backup_hosts",
                    "item_variable": "host",
                    "body": {
                      "environment": { "scope": "inherit_parent" },
                      "module": {
                        "cyborg.modules.borg.repository.v1": {
                          "hostname": "${host.hostname}",
                          "port": "${host.port}",
                          "repository_root": "${host.borg_repo_root}",
                          "repository_name": "${container_name}",
                          "ssh_command": "${host.borg_rsh}",
                          "passphrase_variable": "secrets.passphrase",
                          "body": {
                            "environment": { "scope": "parent" },
                            "module": {
                              "cyborg.modules.sequence.v1": {
                                "steps": [
                                  {
                                    "environment": { "scope": "parent" },
                                    "module": {
                                      "cyborg.modules.borg.create.v1": {
                                        "archive_name_pattern": "${container_name}-{now}",
                                        "paths": [ "${volume_root}" ],
                                        "compression": { "algorithm": "zlib" },
                                        "exclude_patterns": [
                                          "*/mongo/diagnostic.data/*",
                                          "*/sharelatex/tmp/*",
                                          "*/sharelatex/data/cache/*",
                                          "*/sharelatex/data/compiles/*",
                                          "*/sharelatex/data/output/*"
                                        ],
                                        "exclude_caches": true
                                      }
                                    }
                                  },
                                  {
                                    "environment": { "scope": "parent" },
                                    "module": {
                                      "cyborg.modules.borg.prune.v1": {
                                        "glob_archives": "${container_name}-*",
                                        "retention": {
                                          "keep_daily": 30,
                                          "keep_weekly": 12,
                                          "keep_monthly": 12
                                        }
                                      }
                                    }
                                  },
                                  {
                                    "environment": { "scope": "parent" },
                                    "module": {
                                      "cyborg.modules.borg.compact.v1": {}
                                    }
                                  }
                                ]
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            ]
          }
        }
      },
      "finally": {
        "environment": {
          "scope": "reference",
          "name": "overleaf_job"
        },
        "module": {
          "cyborg.modules.docker.up.v1": {
            "compose_path": "${container_root}/docker-compose.yml",
            "user": "${docker_user}"
          }
        }
      }
    }
  }
}
```

**Environment Flow:**
```
1. "overleaf_job" scope created (inherit_parent from global)
2. Configuration runs → sets secrets.passphrase, container_name, etc.
3. Guard body uses "parent" → same "overleaf_job" scope
4. Docker down reads ${container_root}, ${docker_user} from current scope
5. ForEach creates iteration scopes (inherit from "overleaf_job")
   → Each iteration has ${host} + all parent variables
6. Repository module sets BORG_REPO, BORG_PASSPHRASE in iteration scope
7. Borg create/prune/compact read BORG_* from environment
8. On completion or error: finally references "overleaf_job" scope
   → Reads ${container_root}, ${docker_user} to restart containers
```

### Example: SMB Data Daily Backup (`jobs/daily/smb-data.json`)

This example demonstrates:
- Nested foreach loops (smb_targets → backup_hosts)
- Per-iteration secrets loading via configuration block
- Conditional execution based on directory existence
- Named guard scope for service restart

```json
{
  "environment": {
    "scope": "inherit_parent",
    "name": "smb_job"
  },
  "configuration": {
    "cyborg.modules.config.map.v1": {
      "entries": [
        { "key": "service_name", "string": "smbd.service" }
      ]
    }
  },
  "module": {
    "cyborg.modules.guard.v1": {
      "body": {
        "environment": { "scope": "parent" },
        "module": {
          "cyborg.modules.sequence.v1": {
            "steps": [
              {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.systemd.stop.v1": {
                    "service_name": "${service_name}"
                  }
                }
              },
              {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.foreach.v1": {
                    "collection": "smb_targets",
                    "item_variable": "target",
                    "continue_on_error": false,
                    "body": {
                      "environment": {
                        "scope": "inherit_parent",
                        "name": "smb_target_iteration"
                      },
                      "configuration": {
                        "cyborg.modules.secrets.load.v1": {
                          "source": "${target.secrets_file}",
                          "source_type": "json_file",
                          "environment_prefix": "secrets"
                        }
                      },
                      "module": {
                        "cyborg.modules.if.v1": {
                          "condition": {
                            "variable": "target.root",
                            "operator": "exists"
                          },
                          "then": {
                            "environment": { "scope": "parent" },
                            "module": {
                              "cyborg.modules.foreach.v1": {
                                "collection": "backup_hosts",
                                "item_variable": "host",
                                "body": {
                                  "environment": { "scope": "inherit_parent" },
                                  "module": {
                                    "cyborg.modules.borg.repository.v1": {
                                      "hostname": "${host.hostname}",
                                      "port": "${host.port}",
                                      "repository_root": "${host.borg_repo_root}",
                                      "repository_name": "${target.repository_name}",
                                      "ssh_command": "${host.borg_rsh}",
                                      "passphrase_variable": "secrets.passphrase",
                                      "body": {
                                        "environment": { "scope": "parent" },
                                        "module": {
                                          "cyborg.modules.sequence.v1": {
                                            "steps": [
                                              {
                                                "environment": { "scope": "parent" },
                                                "module": {
                                                  "cyborg.modules.borg.create.v1": {
                                                    "archive_name_pattern": "${target.repository_name}-{now}",
                                                    "paths": [ "${target.root}" ],
                                                    "compression": { "algorithm": "zlib" },
                                                    "exclude_caches": true
                                                  }
                                                }
                                              },
                                              {
                                                "environment": { "scope": "parent" },
                                                "module": {
                                                  "cyborg.modules.borg.prune.v1": {
                                                    "glob_archives": "${target.repository_name}-*",
                                                    "retention": {
                                                      "keep_daily": 30,
                                                      "keep_weekly": 24,
                                                      "keep_monthly": 24
                                                    }
                                                  }
                                                }
                                              },
                                              {
                                                "environment": { "scope": "parent" },
                                                "module": {
                                                  "cyborg.modules.borg.compact.v1": {}
                                                }
                                              }
                                            ]
                                          }
                                        }
                                      }
                                    }
                                  }
                                }
                              }
                            }
                          },
                          "else": {
                            "environment": { "scope": "parent" },
                            "module": {
                              "cyborg.modules.log.v1": {
                                "level": "warn",
                                "message": "SMB root '${target.root}' does not exist, skipping"
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            ]
          }
        }
      },
      "finally": {
        "environment": {
          "scope": "reference",
          "name": "smb_job"
        },
        "module": {
          "cyborg.modules.systemd.start.v1": {
            "service_name": "${service_name}"
          }
        }
      }
    }
  }
}
```

**Environment Flow:**
```
1. "smb_job" scope created (inherit_parent from global)
2. Configuration sets service_name = "smbd.service"
3. Guard body uses parent → same "smb_job" scope
4. Systemd stop reads ${service_name}
5. Outer foreach creates "smb_target_iteration" scopes for each target
   → Configuration loads secrets.passphrase per-target
6. If condition checks ${target.root} existence
7. Inner foreach creates host iteration scopes (inherit from target scope)
   → Each has ${host} + ${target} + ${secrets.passphrase}
8. Repository sets BORG_* vars, borg modules execute
9. Finally references "smb_job" to read ${service_name} for restart
```

---

## Global Configuration (`config.json`)

The root configuration establishes the global environment with backup hosts and orchestrates WoL → template execution → cleanup.

```json
{
  "environment": {
    "scope": "global",
    "name": "global"
  },
  "configuration": {
    "cyborg.modules.config.map.v1": {
      "entries": [
        {
          "key": "backup_hosts",
          "collection": [
            {
              "hostname": "backup1.service.local",
              "port": 22,
              "wake_on_lan_mac": "11:22:33:44:55:66",
              "borg_rsh": "/usr/bin/sshpass -f/root/.ssh/pass -P assphrase /usr/bin/ssh",
              "borg_repo_root": "/var/backups/borg/nas1.service.local"
            },
            {
              "hostname": "backup2.service.local",
              "port": 22,
              "wake_on_lan_mac": "aa:bb:cc:dd:ee:ff",
              "borg_rsh": "/usr/bin/sshpass -f/root/.ssh/pass -P assphrase /usr/bin/ssh",
              "borg_repo_root": "/var/backups/borg/nas1.service.local"
            }
          ]
        },
        {
          "key": "allowed_run_as_users",
          "collection": [ "docker", "backup" ]
        }
      ]
    }
  },
  "module": {
    "cyborg.modules.sequence.v1": {
      "steps": [
          {
            "environment": { "scope": "global" },
            "module": {
              "cyborg.modules.foreach.v1": {
                "collection": "backup_hosts",
                "item_variable": "host",
                "body": {
                  "environment": { "scope": "inherit_global" },
                  "module": {
                    "cyborg.modules.wol.wake.v1": {
                      "hostname": "${host.hostname}",
                      "mac_address": "${host.wake_on_lan_mac}",
                      "ssh_port": "${host.port}",
                      "state_variable": "wol_state.${host.hostname}"
                    }
                  }
                }
              }
            }
          },
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.template.v1": {
              "templates": [
                { "name": "daily", "path": "jobs/daily.json" },
                { "name": "weekly", "path": "jobs/weekly.json" },
                { "name": "monthly", "path": "jobs/monthly.json" }
              ]
            }
          }
        },
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.foreach.v1": {
              "collection": "backup_hosts",
              "item_variable": "host",
              "body": {
                "environment": { "scope": "inherit_global" },
                "module": {
                  "cyborg.modules.ssh.shutdown.v1": {
                    "hostname": "${host.hostname}",
                    "port": "${host.port}",
                    "ssh_command": "${host.borg_rsh}",
                    "state_variable": "wol_state.${host.hostname}"
                  }
                }
              }
            }
          }
        }
      ]
    }
  }
}
```

**Environment Flow:**
```
1. Global environment receives backup_hosts collection, allowed_run_as_users
2. WoL module iterates hosts, sets wol_state.{hostname} in global scope
   → Uses inherit_global so state variables persist
3. Template module executes selected job (daily/weekly/monthly)
   → Job inherits global environment (backup_hosts, wol_state.*, etc.)
4. Shutdown module reads wol_state.{hostname} from global
   → Only shuts down hosts that were woken by this run
```
