# Transaction Core

> **Status:** Internal implementation design for the transaction coordinator and generic participant model.

## Responsibility

The transaction core owns execution-state topology and reconciliation. It does not know environment variables, named modules, artifacts, or custom service semantics. Those concerns participate through descriptors and transaction-local participant state.

The core is responsible for:

- creating independent root transactions from one participant registry and an immutable execution seed;
- opening stable fork groups without rebasing or clearing owner provenance;
- creating an explicit owner-continuation branch and zero or more child branches from the same participant baselines;
- enforcing structured fork/transaction lifetime rules;
- asking every participant to prepare a detached candidate during join;
- publishing one complete aggregate participant-state bundle only after every participant prepares successfully;
- applying a common conflict-strategy boundary without embedding component-specific conflict keys in the coordinator.

The core remains independent of DI and module execution in this stage. Runtime integration binds one of these transaction nodes to each invocation scope later.

## Participant Boundary

A participant descriptor is the stable identity for one transactional concern. The descriptor contains no mutable workflow state. It creates root participant state from the immutable seed supplied for a root execution.

Participant state belongs to one transaction node and owns component-specific local mutation semantics. When the transaction opens a fork, the participant state produces a fork object that captures the stable component baseline for that fork generation.

The fork object has two responsibilities:

1. create isolated branch state over the captured baseline;
2. prepare a detached owner-state candidate from completed contributor states and the selected conflict strategy.

This keeps topology and component semantics separate:

```text
transaction coordinator
  owns topology, contributor ordering, lifecycle, aggregate publication

participant descriptor/state/fork
  owns root seeding, component baseline/change semantics,
  logical conflict detection, candidate construction
```

Participant descriptors are compared by identity inside one coordinator. Registration order is deterministic for diagnostics and preparation, but successful semantics cannot depend on it because preparation cannot publish owner-visible state.

## Root Seeds

One coordinator can create multiple independent roots. Root-specific workflow inputs are supplied through an immutable seed keyed by participant descriptor.

```text
application participant registry
             |
             +-- root seed A -> root transaction A
             +-- root seed B -> root transaction B
```

The seed container is immutable. A participant may use its seed value to construct root state or fall back to its empty/default root state when no execution-specific seed is supplied.

The seed is an input to the transaction tree, not a mutable backing store beneath it.

## Fork Groups

Opening a fork group freezes the owner's effective participant state for that generation. The owner itself becomes unavailable for state access until the fork closes. All post-fork work therefore executes through one of the branches created from the frozen baseline.

Every group contains an explicit continuation branch plus child branches:

```text
owner transaction
      |
      +-- fork baseline S
             +-- contributor 0: owner continuation
             +-- contributor 1: child A
             +-- contributor 2: child B
```

Contributor ordering is stable: continuation first, followed by children in creation order. Conflict strategies can use these contributor identities when a participant exposes a conflict between multiple writers.

A branch can open and close its own nested fork groups before it completes. The owning fork cannot reconcile while any contributor remains active, and a transaction cannot complete or discard while it owns an open nested fork.

## Join and Atomic Publication

A join first collects the completed contributor state bundles. Each participant receives only the states belonging to that participant and prepares a detached candidate.

```text
PREPARE
  participant A -> candidate A
  participant B -> candidate B
  participant C -> conflict

result: discard candidates A/B, owner bundle unchanged
```

No candidate is installed during preparation. If every participant succeeds, the coordinator constructs one candidate aggregate bundle and replaces the owner's state-bundle reference once.

```text
PREPARE ALL
      |
      v
candidate aggregate bundle
      |
      v
single owner publication
```

Participant state objects can use persistent storage and structural sharing internally. Atomicity therefore means atomic publication of the aggregate state identity, not deep copying all component data.

A conflict closes the fork generation without publishing anything. Its contributor transactions become unusable and the owner resumes from the exact pre-fork aggregate state. An unexpected participant preparation failure follows the same publication rule, closes the fork as failed, releases the owner unchanged, and propagates the original exception.

## Conflict Strategy

Participants define logical conflict keys because only the participant understands what constitutes one state location. The transaction core supplies a conflict-strategy abstraction and a common conflict description containing:

- the participant that owns the conflict;
- the participant-defined logical key;
- the contributors that modified that key.

The default strategy fails on conflict. The boundary also permits a strategy to select one conflicting contributor, which allows later policies to be introduced without changing transaction topology or publication semantics.

The strategy can select a candidate result, but it cannot publish state. Aggregate publication always remains a coordinator responsibility.

## Lifecycle

Transaction branches have four semantic states:

```text
Active -> Completed -> Joined
   |
   +----------------> Discarded
```

A completed branch is immutable from the caller's perspective and can only be consumed by its owning fork. Joined and discarded branches are terminal.

Fork groups similarly become joined, discarded, conflict-closed, or failed. Closing a fork releases the owner for further work. Reusing a closed group, joining a contributor twice, or accessing a terminal transaction is a lifecycle error.

Each transaction node and fork group has one logical coordinator/owner. Concurrent execution happens across distinct branch transactions rather than by mutating one transaction object concurrently. The core therefore does not use internal locking for branch-local mutation; structured completion establishes the join point before contributor state is read for reconciliation.

## Transactional Dictionary Integration

The generic transactional dictionary provides the map semantics used by synthetic transaction participants and later workflow-state components:

- persistent immutable fork baselines;
- explicit final `Set` / `Remove` operations;
- durable parent-relative provenance;
- negative caching for removals;
- detached merge-candidate construction.

The dictionary deliberately does not own transaction topology. A map-backed participant translates dictionary write conflicts into participant conflict descriptions and applies the coordinator's selected strategy before producing its detached candidate.

This separation keeps the collection reusable when one higher-level participant contains several maps or additional non-map topology.

## Runtime Integration Boundary

The transaction core is ready to be bound to module invocation scopes once the generic semantics are accepted. That integration should add a scoped transaction-context facade rather than exposing coordinator internals to workers or custom modules.

Runtime-owned state components then register as participants and scoped runtime services resolve their current component state through the invocation's transaction context. The coordinator remains the only owner of fork/join lifecycle and aggregate publication.
