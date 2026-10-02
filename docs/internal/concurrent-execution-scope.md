# Concurrent Execution Scopes and Sidecar Modules

This note is the design for invocation-scoped concurrent child execution and the sidecar module built on top of it. The steady-state description lives in [Transactional Execution](../architecture/transactions.md), [System Architecture](../architecture/architecture-overview.md), and [Module Reference](../architecture/modules-reference.md).

## Problem

`cyborg.modules.parallel.v1` starts every branch together, waits for every branch, and only then returns. The owning invocation cannot read a single child result early, cannot cancel one child without canceling the call, and cannot make its own transactional writes while the children run. The fork continuation exists for those writes, but `ExecuteConcurrentlyAsync` completes that continuation immediately and leaves it empty.

Sidecar workflows need a different lifetime without a second transaction model. A primary module and zero or more companion modules run as ordinary nested invocations. The companions are not peers: when the primary reaches a terminal status, companions that are still running are canceled. The group does not finish until every child has terminated and the fork has reconciled. An unexpected companion failure cancels the remaining children and fails the group. Otherwise the group's result is the primary result.

That policy is a module concern. The runtime must not learn what a sidecar is.

## Runtime abstraction

`IModuleRuntime.OpenConcurrentExecution` opens an `IConcurrentExecutionScope` on the calling invocation.

- `StartAsync` starts one normal nested invocation: its own transaction, DI scope, execution id, and lifecycle. The returned `IConcurrentModuleExecution.Completion` task produces that child's result before the scope closes. `Cancel` cancels that child only.
- While the scope is open, the owning invocation keeps running. Reads and writes made through its runtime, environment views, module registry, and transaction-aware services go to the fork continuation.
- Children stay isolated from the owner and from each other until close. A child forked at `StartAsync` does not observe later continuation writes. Nested sequential execution performed by the owner forks from the continuation, so it does observe those writes, and its join becomes part of the continuation.
- `CloseAsync` waits until every started child has terminated, completes each child transaction under that module's failure-publication policy, completes the continuation, and reconciles the fork. Child scopes are disposed only after that join or discard. `Closed` is emitted after the structural outcome is known, which is why a child can already be `Completed` while the scope is still open.
- Disposing a scope that was not closed cancels every child, waits for termination, and discards the fork. Owner state stays at the pre-fork baseline.
- The scope is agnostic to why a child was started or canceled. It does not rank children, interpret exit status, or apply sidecar policy.

Opening a scope retargets the invocation's `ActiveTransaction` at the continuation. Child invocations receive a new `ActiveTransaction` fixed to their own transaction, so retargeting the owner does not move a child. Closing or discarding the scope retargets the owner back to its transaction, which then holds the published state or the untouched pre-fork state. The owner transaction object stays frozen for direct access while the fork is open; the active pointer is what makes the continuation the invocation's current transactional view.

Nested scopes are the same operation on the current transaction. The inner scope's owner is the outer continuation. Scopes close from the inside out. A scope cannot close while its continuation still has an open nested fork.

Debugger branch control keeps its conflict-free merge. An untouched pre-fork continuation is still ignored when children exist, so that copy cannot resurrect stepping. A continuation whose generation or step flag changed during the scope is an owner decision and is included.

`ExecuteConcurrentlyAsync` is this scope with an empty continuation, every context started up front, and one wait before close. Parallel modules do not grow a private join path.

## Sidecar module

`cyborg.modules.sidecar.v1` has a required primary `module` context and zero or more `sidecars` contexts. The worker opens one concurrent execution scope, starts the primary and every sidecar, and then applies lifetime policy:

- The primary reaching any terminal status cancels sidecars that are still running.
- A sidecar result of `Failed`, or a sidecar invocation that faults before a definite result, is unexpected. The worker cancels the primary and the other sidecars.
- `Success`, `Skipped`, and `Canceled` are not sidecar failures. `Canceled` is the expected result of shutting a companion down, and of caller cancellation flowing into the group.
- The worker closes the scope only after every child task has finished, so transactional state is reconciled before the sidecar module itself returns.
- If any sidecar result is `Failed`, the sidecar module returns `Failed`. A fault with no definite child result still discards the fork through scope disposal, and the invocation fails.
- Otherwise the sidecar module returns the primary status. Child artifacts and other workflow writes are published only by reconciliation, under each child's own `Transaction.OnError`.

The worker does not complete transactions, join forks, or dispose child scopes. Caller cancellation is an ordinary child token passed to `StartAsync`; it is not a separate sidecar rule.

## Failure and nesting

Each child still resolves `Commit` or `Rollback` from its own module when its transaction completes at scope close. Rollback withholds that child's workflow data and still reconciles control state. A conflict or preparation failure publishes nothing for the whole scope, including continuation writes made by the owner.

An inner sidecar or parallel module joins inside its own invocation. Those writes are just that child's contribution to the outer scope. An outer sequential module sees the sidecar invocation as one normal child. No scope stays open across an invocation boundary: a worker that returns without closing its scope is cleaned up by disposal, which discards rather than publishing a partial fork.

## Compatibility

Existing sequential execution does not open a scope. Existing parallel execution still starts every branch from one baseline, waits for every branch, preserves declaration order, and fails the module on conflict without publishing. Callers that never open a scope do not observe continuation retargeting.
