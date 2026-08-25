# Environment and Runtime State

> **Status:** Internal target design. See [Transactional Execution Design](README.md) for scope and invariants.

## Responsibility

The environment subsystem is the primary built-in transactional component. It owns logical environment identity, inheritance topology, named registration, variable bindings, and the workflow-state effects of artifact publication.

The current conceptual split between mutable environment objects and a separate runtime catalog is replaced by one coherent transaction-owned graph. A runtime environment exposed to module code is a view over a logical environment identity in the current transaction rather than the owner of a mutable backing dictionary.

Runtime named-module registrations form a second built-in transactional component because they share the same execution visibility rules but contain different domain state.

## Environment Graph

Each environment has an internal logical identity independent of its optional user-facing name. Conceptually, environment component state contains:

```text
environment graph
  logical global environment id
  environment nodes: EnvironmentId -> node state
  named catalog:      string -> EnvironmentId

environment node state
  optional parent EnvironmentId
  transient/lifetime metadata
  persistent variable bindings
```

Topology, registration, and bindings are reconciled coherently. A named registration cannot report failure while independently replacing an environment node, and an inherited relationship cannot point at a mutable environment object owned by another transaction.

## Environment Views

`IRuntimeEnvironment` remains the module-facing abstraction for variable resolution and publication, but its implementation becomes a transaction-bound view.

A view conceptually carries:

- access to the scoped transaction context;
- one logical `EnvironmentId`;
- the currently bound namespace;
- override-resolution tags and other view-level metadata.

`Bind(namespace)` creates another view of the same logical environment identity. It does not clone or alias a mutable dictionary.

All reads resolve environment state through the current transaction so a parent-scoped view can observe a successful nested join through the same stable transaction handle.

## Scope Semantics

Existing environment scope behavior remains user-visible, but every operation is interpreted inside the current transaction snapshot:

| Scope | Transactional interpretation |
|---|---|
| `Isolated` | Create a new environment identity with no parent. |
| `InheritParent` | Create a new identity whose parent is the caller's current logical environment. |
| `Global` | Bind to this execution tree's logical global environment identity. |
| `InheritGlobal` | Create a new identity whose parent is the execution tree's logical global identity. |
| `Parent` | Bind to the caller's logical environment identity. |
| `Current` | Bind to the current logical environment identity at the invocation boundary. |
| `Reference` | Resolve a user-visible name through the current transaction's named catalog. |

No scope operation obtains a live environment object from another transaction and mutates it directly.

## Logical Global Environment

The logical global environment is seeded into each root execution. It is global only within that transaction tree.

```text
application provider
  +-- execution A -> global environment A
  +-- execution B -> global environment B
```

Both roots may start from the same immutable seed data, but mutations in one execution cannot become visible in the other.

This preserves the existing workflow concept of a `Global` scope without retaining a process-global mutable `GlobalRuntimeEnvironment` as workflow-state ground truth.

## Variable Bindings and Change Keys

For environment variables, the logical conflict key is:

```text
(EnvironmentId, variable path)
```

A node's variable bindings use the persistent baseline/change model:

```text
baseline binding map
local changes:
  key -> Set(value)
  key -> Remove
```

Reads check the local change set and then the immutable baseline. Inherited lookup follows `ParentEnvironmentId` within the same transaction snapshot.

A successful local write or removal stays dirty for the transaction lifetime, even when later operations restore the original visible value or absence.

### Deletions

Removal must represent explicit absence in local transactional state so deletion can reconcile correctly against a baseline-local binding. It is therefore modeled as an explicit `Remove`/tombstone operation rather than by simply deleting an entry from the local change map.

Environment-local removal semantics do not automatically become recursive hiding semantics. If a variable exists only in an inherited parent, removing it from the child behaves according to the existing environment contract unless a separate hide/negative-override feature is deliberately introduced.

## Resolution, Overrides, and Interpolation

The transaction migration changes storage and visibility, not the established resolution contract.

Variable resolution, module-property overrides, interpolation, tags, namespaces, and decomposition continue to use the semantics documented in [System Architecture](../../architecture/architecture-overview.md) and [Interpolation and Override Resolution](../../architecture/interpolation.md).

The important boundary is that every lookup traverses one transaction-consistent environment graph. An inherited read cannot fall through to a mutable parent environment object that has changed after the child forked.

## Environment Creation and Registration

Creating an environment records topology in the current transaction. Registering a non-transient name records a catalog change in the same component.

Logical conflict keys include at least:

- named environment registration name;
- environment identity/topology where independently mutable topology can conflict;
- individual variable bindings.

The environment component prepares all related candidate graph changes together. A registration conflict therefore leaves both the catalog and candidate environment topology unchanged in the owner.

## Transient Environment Lifetime

Module-local environments should not accumulate in ancestor transactions merely because they existed during child execution.

On successful reconciliation:

- changes to environment identities already reachable in the owner reconcile normally;
- newly named/non-transient environments that become owner-visible are retained;
- child-local transient identities that are not reachable from surviving state are discarded;
- if a surviving environment inherits through an otherwise-transient ancestor, the topology required to preserve that relationship remains internally reachable even when the ancestor has no name.

Reachability is therefore an environment-component concern, not a generic transaction rule.

## Artifact Publication

Artifacts are not a separate transactional component. They are staged values written into the environment component.

A module builds its artifact collection locally during execution. On module exit, the configured artifact target is resolved through the **current transaction's** environment graph, including `Parent`, `Current`, `Global`, and named references. Publication writes the resulting bindings into that transaction.

The transaction may contain logical identities corresponding to caller environments, so `Parent` publication does not require mutating the caller's live runtime/environment object.

Artifacts become visible to the caller only when the child transaction reconciles successfully.

This preserves existing artifact scope semantics while making publication isolation consistent with ordinary variable writes.

## Opaque Values

Cyborg tracks binding changes, not mutation inside values:

```text
(EnvironmentId, key) -> object reference / absent
```

A child can safely replace a binding without exposing that replacement before join. If two transactions receive the same mutable object reference from their immutable baseline and mutate that object internally, those mutations are outside Cyborg's transaction model.

Values crossing concurrent execution boundaries should therefore be immutable or provide their own synchronization when shared mutation is intentional.

## Static Named Modules

Named module definitions discovered while loading a static workflow belong to the immutable loaded graph/seed, not to a singleton runtime registry.

The load path builds an immutable initial named-module catalog associated with the workflow definition. Starting a root execution seeds the runtime named-module transactional component from that catalog.

A module reference stored in the catalog is an immutable loaded definition plus activation identity, never a preconstructed worker.

## Runtime Named Modules

Dynamic configuration modules can load/register module definitions during execution. Those operations use a scoped facade over the current transaction's named-module component.

A dynamic registration:

- is immediately visible inside the transaction that created it;
- is invisible to siblings before reconciliation;
- becomes visible to the parent after successful join;
- conflicts with another contributor changing the same logical name under the default merge strategy.

The logical conflict key is the runtime module name. Removal is represented explicitly for the same reason as environment deletion.

## Interaction Between Environment and Named-Module State

Environment and named-module state are separate transactional components because their domain semantics differ, but their publication is one transaction operation.

If a fork generation successfully prepares environment changes but encounters a conflicting named-module registration, neither component becomes visible in the owner. This cross-component atomicity is supplied by the transaction coordinator rather than by coupling the two component implementations.
