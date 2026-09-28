# Failure Publication for Module Transactions

This note is the design for configurable commit/rollback when a module invocation fails. The steady-state description lives in [Transactional Execution](../architecture/transactions.md) and [Module Reference](../architecture/modules-reference.md).

## Problem

Every completed invocation currently joins its child transaction into the owner, including `Failed` and `Canceled` results. Exceptional completion with no result still discards the fork. Exit status and publication are therefore different facts, but publication has no policy: a failed child always keeps its workflow writes.

Callers that want attempt isolation (retries) or failure containment have to avoid writing, or undo writes by hand. That does not compose with nested transactions, named-module registration, or custom transactional services.

## Policy

`ModuleBase.Transaction.OnError` is an optional `commit` or `rollback` setting.

- Omitted uses the global default `cyborg.core.transactions.on_error`.
- The built-in default is `commit`, which is today's behavior: a completed child joins into its parent.
- `rollback` withholds the child's transactional workflow data. The parent keeps the pre-fork workflow state for that child.
- `Success` and `Skipped` always join. The setting is an on-error policy, not a success policy.
- `Failed` and `Canceled` are error outcomes and consult the policy. Cancellation that escapes before a definite result still discards the fork, as it does today.
- A missing configuration provider, or a module instance that never ran validation, resolves like an omitted setting and uses `commit` when no provider is registered.

The typed configuration value is `cyborg.types.core.transactions.on_error.v1`. A module setting wins over the global default. `Transaction` is ignored by the override pipeline, so an environment value cannot replace the invocation's policy. `OnError` is an enum, so it is not an interpolation target.

## What rollback withholds

Rollback applies only to **workflow-data** participants:

- the runtime environment graph and bindings;
- the runtime named-module registry;
- custom `TransactionalServiceParticipant<TState>` services, which are workflow-semantic unless they say otherwise.

**Control** participants still reconcile. Debugger branch-control state is the built-in control participant: step and continue decisions survive a workflow rollback so a failed, rolled-back child cannot trap the branch in a stale step mode or drop a step that the child took. Custom participants opt into this class by overriding `TransactionalServiceParticipant.Role` to `TransactionParticipantRole.Control`. The default role is `WorkflowData`.

External I/O, process-wide singletons, and mutation inside objects stored as environment values stay outside the transaction, for both policies.

## Mechanism

The fork group still prepares one aggregate candidate and publishes it atomically, or publishes nothing on conflict. Rollback does not add a second publication path and does not discard the fork.

Before preparation, a child completed with workflow rollback contributes a fresh baseline branch for every workflow-data participant and its real post-execution state for every control participant. The baseline branch is created from the fork captured at open, so it carries no change provenance. Control state merges with the existing conflict-free rules. Because the substituted workflow state records no writes, siblings cannot conflict with a rolled-back child, and the owner's pre-fork workflow values remain.

`Closed.Joined` stays `true` for this outcome. The fork reconciled. Joined means the invocation was not discarded; it does not mean every participant published child writes. Full discard remains the path for an incomplete child or a preparation failure, and that path still publishes nothing, including control state.

Sequential `ExecuteAsync` applies the policy of the invocation's module (the context's main module, the referenced module, or the activated worker's module) to that child transaction. A configuration module nested inside the context is its own invocation and applies its own policy when it joins the context transaction. The context's policy then decides whether the context transaction, including anything already joined into it, publishes to the caller.

`cyborg.modules.parallel.v1` uses the same rule per branch. Each branch is one contributor. A rolled-back branch contributes baseline workflow state; a committed branch contributes its writes. One shared join still publishes all contributors atomically.

## Nested composition

Inner policy decides what enters the outer transaction. Outer policy decides what leaves it. The two settings do not override each other.

`cyborg.modules.retry.v1` is the first consumer. It runs its `body` up to `attempts` times (`attempts` ≥ 1):

- the first `Success` completes the retry as `Success`;
- `Canceled` stops the retry immediately as `Canceled`;
- any other status consumes an attempt;
- exhaustion completes the retry as `Failed`.

A body with `OnError = commit` leaves failed-attempt workflow writes in the retry transaction, so the next attempt forks from that state. A body with `OnError = rollback` contributes no workflow writes, so attempts are isolated. The retry module's own `OnError` applies only when the retry invocation itself joins its parent, and only if the retry outcome is `Failed` or `Canceled`. A successful retry always publishes the attempts that committed into it, regardless of the retry module's on-error setting.

## Compatibility

Existing modules omit the setting. The global default is `commit`. Sequential visibility, parallel conflict detection, artifact publication, and named-module reconciliation are unchanged until a module or the process default selects `rollback`.
