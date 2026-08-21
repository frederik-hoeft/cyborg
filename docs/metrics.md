# Metrics

Cyborg writes Prometheus exposition data to the configured metrics file after each CLI run. The default namespace is `cyborg`, so metric names are emitted with the `cyborg_` prefix unless a different namespace is configured.

Metric labels may be supplied as `TaggedString` values. The metrics subsystem renders those labels through `ITaggedStringRenderer` before writing exposition data, so secret-tagged labels use the same redaction policy as other Cyborg-controlled presentation surfaces.

## Global run status

The CLI emits the following global gauge independently of module-specific metrics:

```text
cyborg_last_run_success
```

The value is:

- `1` when the top-level workflow returns `Success`;
- `0` when the workflow fails, is skipped, is canceled, or terminates with an exception after metrics initialization.

The metric is written from the CLI run boundary rather than from an individual module. This means failures that occur before module execution or module-level metric collection, including main-module loading and deserialization failures, are still visible to Prometheus.

Metrics initialization depends on successfully loading the CLI options configuration. Failures that occur before the metrics namespace and destination can be resolved cannot update the metrics file.
