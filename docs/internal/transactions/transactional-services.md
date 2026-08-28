# Transactional Services

> **Status:** Internal architecture notes. See [Transactional Execution Design](README.md) for scope and invariants.

## Responsibility

Transactional service participation lets selected runtime and custom DI services carry workflow-semantic state through the same transaction tree as environments and runtime registries.

Participation is explicit. DI lifetime by itself does not imply that a service's mutable object state is inherited, cloned, or merged.

## Service Categories

Services fall into three architectural categories:

1. **Immutable/stateless application infrastructure** such as parsers, serializers, loader registries, syntax factories, and generated activation metadata. These can remain singleton.
2. **Ordinary shared mutable infrastructure** such as process-wide metrics, debugger registries, or caches. These may remain singleton but must provide their own thread safety. Transaction isolation does not apply to them.
3. **Workflow-semantic state** such as environment bindings, runtime named-module registrations, or custom execution state that must fork and reconcile with module execution. These explicitly participate in transactions.

A service should not be made transactional to avoid normal concurrency discipline for shared process infrastructure.

## Participant Model

Each transactional concern has two sides:

```text
application-level participant descriptor
  defines root seeding, fork snapshot semantics,
  conflict keys, and reconciliation preparation

invocation-scoped service facade
  accesses the participant state belonging to
  the current transaction through scoped context
```

The mutable service object is not itself copied between transactions. Transaction state belongs to the transaction node; service facades are access paths into that state.

This separation allows a custom service to choose any internal state representation while preserving common transaction topology and atomic publication.

## Service-Facing Extension Surface

Custom service participation is exposed through a typed descriptor/fork contract rather than the internal transaction coordinator. A participant derives from `TransactionalServiceParticipant<TState>` and defines:

- creation of a fresh root state for each independent execution root;
- creation of a stable `TransactionalServiceFork<TState>` from the current owner state;
- isolated branch-state construction;
- detached merge preparation and logical conflict detection.

Participant descriptors are registered as application singletons under the non-generic `TransactionalServiceParticipant` service type. The root runtime adapts those descriptors into its internal participant bundle. Descriptor identity and transaction topology remain runtime concerns; custom services do not receive `ExecutionTransaction` or join handles.

A transactional service facade consumes the scoped `ITransactionalServiceContext` and obtains an `ITransactionalServiceState<TState>` handle for its participant type. The handle is stable for the lifetime of that DI service, but resolves the current transaction-owned component state for every read or mutation. This gives a parent-scoped facade access to newly reconciled child state without recreating the parent DI scope. It also means an attempted parent mutation while a fork is open is rejected by the same transaction lifecycle guard as built-in state.

The state object supplied to a handle callback is participant-owned and should not be retained by the service facade. Retaining the stable handle is the supported pattern.

Custom forks receive `ITransactionalServiceConflictResolver` during merge preparation. Contributor index `0` identifies the owner-continuation branch; remaining indices identify child branches in fork creation order. A participant reports the logical key and the indices that conflict, and the runtime conflict strategy either selects one contributor or rejects the merge. The participant remains responsible for constructing the detached candidate from the selected changes.

An unresolved logical conflict is the only normal `false` result from custom merge preparation. A participant must report that conflict through the supplied resolver so the coordinator's configured strategy remains authoritative. A participant that cannot prepare a valid candidate for some other reason should fail by throwing; the coordinator closes the fork without publishing any participant candidate.

The public service extension surface therefore consists of state semantics and scoped access only. Root/child topology, participant ordering, aggregate publication, branch lifecycle, and transaction disposal remain internal runtime responsibilities.

## Participant Granularity

A participant should correspond to one independently transactional subsystem or state component. Unrelated workflow state should remain separate participants so each component owns a narrow root seed, state model, fork semantics, conflict keys, and reconciliation policy. The coordinator already provides aggregate atomic publication across participants, so atomicity alone is not a reason to combine unrelated state.

Some subsystem state has an intrinsic reconciliation dependency. In that case, use one thin composite participant only when preparing a valid candidate for one concern requires the candidate state of another. The composite participant should orchestrate smaller focused state/fork components rather than implement every map and policy itself.

The runtime environment is the primary example: graph reconciliation determines which logical environment identities remain reachable, and binding reconciliation must discard changes for pruned identities. The environment participant therefore composes focused graph and binding components, while the named-module registry remains a separate participant because its merge semantics do not depend on the environment candidate.

Do not create a catch-all runtime participant merely because several components are runtime-owned.

## Root State

A custom participant creates fresh root component state from its descriptor's immutable defaults or application-level configuration. The descriptor may depend on ordinary singleton configuration services, but it must not use singleton mutable workflow state as the backing store for multiple roots.

Built-in runtime participants may additionally consume execution-specific root seeds owned by the runtime. That internal seeding mechanism is not exposed through the custom-service extension surface.

There is no singleton mutable participant state that serves as the backing store for all roots.

A participant without application-specific initial state starts from an empty/default state.

## Scoped Access

Every module scope contains a scoped transactional-service context bound before worker activation. A transactional service facade retains a typed state handle obtained from that context. The handle resolves the current participant state for every operation rather than caching the mutable component object itself.

This permits a parent-scoped service instance to observe state reconciled from a completed child without recreating the parent DI scope. It also prevents ordinary facade operations from bypassing owner-freeze semantics while a nested fork is active.

## Forking Service State

When an invocation forks, the transaction coordinator asks every registered participant for a stable effective snapshot or equivalent immutable branch seed. The same snapshot is shared by all sibling branches in that generation.

A child starts with:

```text
participant baseline = parent's fork snapshot
participant changes  = empty
```

The child facade then records changes only in its transaction-local participant state.

Participants with map-like state should use the persistent baseline/change model from [State and Reconciliation](state-and-reconciliation.md). Participants with other state shapes may use a different representation, but must provide equivalent snapshot isolation and explicit change provenance needed by their conflict semantics.

## Reconciliation Contract

A participant prepares a candidate result from:

- the owner's pre-fork component state;
- the owner continuation changes;
- completed child changes;
- the selected conflict strategy.

Preparation cannot mutate parent-visible state. It returns enough candidate state for the coordinator to construct a complete replacement execution-state bundle.

All participants prepare before any candidate becomes visible. This means a conflict in one custom service also prevents otherwise-valid environment or named-module changes from being published by that fork generation.

## Opt-In Experience for Custom Services

The extension surface should make the required semantics explicit without forcing custom services to understand runtime internals.

A custom transactional service provides:

- one singleton `TransactionalServiceParticipant<TState>` descriptor;
- root-state construction from its default/application configuration;
- a `TransactionalServiceFork<TState>` that captures the stable fork point, creates isolated branch state, and prepares reconciliation;
- transaction-local mutation/change tracking appropriate for its state representation;
- logical conflict keys reported through the runtime conflict resolver;
- an ordinary scoped/transient service facade that retains an `ITransactionalServiceState<TState>` handle.

The participant is registered under `TransactionalServiceParticipant` in the application's Jab service module. The runtime consumes the resulting descriptor collection directly through compile-time DI, so reflection-based participant discovery is not required.

The transaction coordinator is responsible for lifecycle, nesting, atomicity, and participant ordering. A custom participant should not manually join child transaction objects or publish itself into parent state.

## Participant Ordering

Successful semantics must not depend on registration order. Preparation may run in a deterministic order for diagnostics, but no participant is allowed to mutate parent-visible state during preparation, so a later failure cannot leave earlier participants partially committed.

Aggregate publication is one coordinator operation after every participant has succeeded.

## DI Lifetime Interaction

Transactional participation and DI lifetime remain orthogonal:

- a singleton participant descriptor can define state semantics without holding mutable workflow state itself;
- a scoped facade gets a fresh service object per module invocation and accesses that invocation's transaction state;
- a transient service can also access transaction state through the scoped context if useful;
- a normal scoped service that does not opt in starts fresh per invocation and is neither inherited nor merged.

This is important because Jab/MEDI scopes do not form a logical parent/child state hierarchy. Cyborg's transaction tree provides ancestry explicitly.

## No Ambient Async Routing

Normal transactional services should not use a process-wide `AsyncLocal` to discover the current transaction. The current invocation DI scope already owns an unambiguous transaction context, and runtime-owned child tasks are created through explicit structured execution APIs.

This avoids two competing propagation mechanisms:

```text
DI scope flow
ambient ExecutionContext / AsyncLocal flow
```

and prevents arbitrary tasks from accidentally inheriting transactional authority merely because .NET propagated an `ExecutionContext`.

## Shared Singletons Under Parallel Execution

Parallel module execution increases concurrency against ordinary singleton services. Shared mutable singleton implementations reached from module workers therefore require ordinary thread safety, not transaction participation, unless their state is itself part of workflow semantics.

Examples:

- the process-wide metrics collector synchronizes concurrent mutation and snapshots;
- debugger breakpoint evaluation and interactive pauses are serialized while the breakpoint registry remains shared;
- workflow variables belong in transactional state rather than a concurrent singleton dictionary.

## Failure Boundary

A transactional participant can fail reconciliation because of a logical conflict or an internal inability to construct a valid candidate. An unresolved logical conflict is reported by returning `false` after the conflict resolver rejects it; an internal preparation failure is represented by an exception. Either failure aborts aggregate reconciliation and leaves the owner state unchanged for that fork group.

Failure while performing external work from a scoped service is different. Transactions cannot undo side effects outside the participant state model; callers must treat such operations according to the module's ordinary failure semantics or a future compensating-action design.
