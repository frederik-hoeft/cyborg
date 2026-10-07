# Debugger `next` (Step Over)

This note is the design for the `next` debugger command. The steady-state description lives in [Workflow Debugging](../architecture/debugging.md) and [Transactional Execution](../architecture/transactions.md).

## Problem

`step` pauses at every prepared module boundary on the current transaction branch, including nested invocations. There is no way to execute the paused module and its subtree, then stop at the next module that is not inside that subtree.

That stop is not a fixed module id. It is the next boundary on the same logical branch after the stepped-over invocation finishes: the following sibling when one exists, otherwise the next boundary on an ancestor after this invocation joins. Parallel siblings are not that successor. Breakpoints inside the subtree still have to pause. A later `step`, `next`, `continue`, or `detach` has to replace the pending instruction.

## Policy

`next` is branch-local execution control on the same transactional participant as `step`. It is not a breakpoint.

- Issuing `next` while paused at invocation `M` clears step-into on that branch and records `M`'s `ModuleExecutionId` as the step-over anchor.
- A later boundary on that branch pauses for `next` only when the anchor is not an open strict ancestor of the boundary. Descendants of `M` are inside the subtree and do not pause for `next`. The first boundary outside that subtree does.
- The anchor is inherited by forks and reconciled on join. An unsatisfied `next` therefore continues on the parent after the anchored invocation, or after a parallel fork, completes. It does not jump to a sibling branch that never received the anchor.
- Persistent and one-shot breakpoints are evaluated independently. A match inside the subtree still pauses. The anchor stays armed until the resume command replaces it.
- `step`, `next`, and `continue` replace one another on the branch that issues them. `step` arms step-into and clears the anchor. `continue` and `cancel` clear both. `detach` advances the session generation, clears breakpoints, and clears the current branch. Stored anchors from the previous generation are invisible and cannot be restored by join.
- Workflow rollback still reconciles this state. `next` is control state, not workflow data. Discard of an invocation that never produced a result still publishes nothing.
- If `next` is never issued, the anchor stays empty and the pause rule is the existing breakpoint-or-step rule.

A `next` resume with no execution id cannot name a subtree. The built-in debugger clears the branch instead of falling through to step-into. Production pauses have an execution id.

## Mechanism

Branch state gains one nullable anchor beside the existing step flag and session generation. The three fields stay mutually exclusive for the built-in control: stepping with no anchor, an anchor and not stepping, or neither. Reads apply the session generation first, so a stale anchor is not visible.

The pre-execution hook already decides to pause before it asks for the frontend. The decision becomes:

```text
should pause = breakpoint decision
               OR current branch is stepping
               OR anchor is set AND anchor is not an open strict ancestor of this invocation
```

Ancestor checks use the live execution topology. `Started` records a node before preparation, and `Closed` removes it after join, before the caller runs another module. A child of `M` still sees `M` in its open parent chain. The following sibling, or a module started after `M` has closed, does not. No module id is predicted in advance, so dynamic, looping, and conditional children need no special cases.

Join stays conflict-free:

- An untouched pre-fork continuation is ignored once real child contributors exist, including when the difference is only the anchor. A continuation that changes generation, step flag, or anchor participates.
- Only the newest session generation can restore stepping or an anchor.
- If any considered contributor is stepping, the parent is stepping and the anchor is cleared. Step-into already pauses at the next boundary, and it must keep pausing inside whatever follows.
- Otherwise the parent keeps one anchor from those contributors. After join, every contributed anchor names an invocation that has completed. The next module on the parent is outside those subtrees, so any one of them resumes the pending `next`. A child that issued a new `step`, `next`, or `continue` replaced the anchor it inherited; the skipped continuation does not put the old one back.

`Continue`, `Step`, and `Next` write the current session generation. `Detach` invalidates the session before clearing the current branch, so the clear is the newest generation.

## Compatibility

Existing `step`, breakpoint, cancel, and detach behavior is unchanged when `next` is not used. The anchor defaults to empty, children still inherit the step flag, and join still restores stepping from any non-stale stepping child. The console command is `next` (`n`). It returns `DebugResumeAction.Next`; `WorkflowDebugger` applies it to the current branch. Frontends still do not mutate transaction state themselves.
