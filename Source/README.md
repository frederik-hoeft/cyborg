# Cyborg - .NET Backup Automation Harness

A modern, modular .NET 10 backup automation harness designed to replace legacy bash scripts with a type-safe, AOT-compiled solution.

## Overview

Cyborg provides a structured, maintainable approach to backup automation with:
- **Modular Architecture**: Pluggable modules for Wake-on-LAN, Docker, Borg, and more
- **Type-Safe Configuration**: JSON configuration with compile-time validation
- **Structured Logging**: JSON-formatted logs for easy parsing and analysis
- **AOT Compilation**: Native ahead-of-time compilation for optimal performance
- **Extensible Design**: Easy to add new modules and sinks

## Architecture

### Projects

- **Cyborg.Core**: Core library with modules, logging, and execution infrastructure
- **Cyborg.Cli**: Console application using ConsoleAppFramework for CLI commands

### Modules

1. **WakeOnLanModule**: Wake remote backup hosts with MAC-based magic packets
2. **DockerModule**: Stop and start Docker containers for consistent backups
3. **BorgModule**: Execute Borg backup operations (create, prune, compact)

### Logging

The logging framework provides an extensible sink-based architecture:
- **ILogSink**: Interface for custom log destinations
- **JsonLogSink**: JSON-formatted console output
- Extension points for file, syslog, or remote logging

## Configuration

Backup jobs are configured via JSON files with the following structure:

```json
{
  "name": "job-name",
  "hosts": [
    {
      "hostname": "backup.example.com",
      "port": 12322,
      "macAddress": "aa:bb:cc:dd:ee:ff",
      "repository": "/path/to/repo"
    }
  ],
  "wakeOnLan": {
    "enabled": true,
    "timeout": 60,
    "retries": 3
  },
  "docker": {
    "user": "docker-user",
    "composePath": "/path/to/docker-compose",
    "containerGroup": "container-group-name"
  },
  "borg": {
    "compression": "zlib",
    "archiveName": "archive-{now}",
    "sourcePath": "/data/to/backup",
    "excludePatterns": [
      "*/cache/*",
      "*/tmp/*"
    ],
    "keepDaily": 30,
    "keepWeekly": 12,
    "keepMonthly": 12
  },
  "passphrase": "borg-repo-passphrase"
}
```

## Usage

### Build

```bash
cd Source
dotnet build
```

### Validate Configuration

```bash
dotnet run --project Cyborg.Cli -- validate path/to/config.json
```

### Run Backup

```bash
dotnet run --project Cyborg.Cli -- run path/to/config.json
```

### Publish AOT Binary

```bash
cd Cyborg.Cli
dotnet publish -c Release -r linux-x64
```

The optimized native binary will be in `bin/Release/net10.0/linux-x64/publish/`.

## Example Configurations

See the `examples/` directory for sample configuration files:
- `overleaf-backup.json`: Overleaf Docker backup with dual remote hosts

## Extending Cyborg

### Adding a New Module

1. Create a class implementing `IModule` or extending `ModuleBase`
2. Implement `InitializeAsync`, `ExecuteAsync`, and `CleanupAsync`
3. Register the module in `BackupOrchestrator`

### Adding a New Log Sink

1. Create a class implementing `ILogSink`
2. Implement `Write(LogEntry)` and `Flush()`
3. Add the sink to the Logger during initialization

## Migration from Bash Scripts

The original bash scripts in the `borg/` directory are preserved for reference. The .NET harness recreates their functionality with:

- Stronger typing and compile-time validation
- Better error handling and recovery
- Structured, parseable logging
- Cross-platform compatibility
- Easier testing and maintenance

## Requirements

- .NET 10 SDK
- Linux host (for Wake-on-LAN, SSH, and Borg)
- Borg Backup installed
- Docker (if using Docker module)
- `wakeonlan` utility (if using WOL module)

## License

See LICENSE file for details.
