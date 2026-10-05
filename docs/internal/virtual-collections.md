# Virtual Collections

This note is the design for dynamically assembled environment collections. The steady-state description lives in [Cyborg Architecture](../architecture/architecture-overview.md#virtual-collections), [Transactional Execution](../architecture/transactions.md#environment-graph-and-bindings), and [Module Reference](../architecture/modules-reference.md#foreach-cyborgmodulesforeachv1).

## Problem

Environment collections are CLR objects stored under an ordinary variable name. A consumer can iterate one only after some earlier step has materialized the whole collection. Workflows that discover elements over time have to rebuild and assign that object, and parallel branches that each produce elements collide on the single binding.

Collections still have to participate in the existing environment model: variable syntax, scoped resolution, transactional commit and rollback, and the validation pipeline that runs when a module binds a value. `IEnvironmentLike` stays the public read/write surface. Callers that already resolve a CLR collection must not learn a second API, and they must not start observing virtual collections by accident.

## Collection names

A virtual collection is addressed by suffixes on a normal variable identifier. The identifier grammar is unchanged. These suffixes are part of variable-reference syntax, not of identifiers, namespaces, or override tags.

| Name | Write | Read |
|------|-------|------|
| `myCollection[]+` | Append the assigned value as one element. Creates the collection when it does not exist. | Not readable. |
| `myCollection[]` | Define or replace the collection. | Stable snapshot taken when the name is resolved. |
| `myCollection[+]` | Not assignable. | Live enumeration of the same collection. |

`myCollection` remains an ordinary variable. A CLR collection stored there is untouched, so existing workflows keep working. The virtual collection of the same identifier is a different binding.

Defining `myCollection[]` makes the collection present even when the definition has no elements. `null`, or any empty non-string sequence, is an empty definition. A successful read of an empty collection returns an empty sequence. A read of a name that was never defined and has no elements fails, which is the same result as an undefined variable. Removing `myCollection[]` or `myCollection[+]` removes that collection from the current environment.

Append stores the assigned object as the element. It does not enumerate it and it does not decompose it. Assigning a sequence to `myCollection[]` is the definition form: the sequence is copied as the new element list. `string` is a single element in both forms, matching the rule that text is not a collection of characters. A definition is materialized before any existing elements are replaced, so the source can be a snapshot of the same collection.

## Reads

Both readable names resolve through `TryResolveVariable`. Snapshot resolution returns the elements copied at that call. Live resolution returns an enumeration that reads the collection again as it moves forward.

The live enumeration is not a subscription. Each step looks at the elements currently visible to that environment. An element appended after the enumerator has passed it is not replayed. An element appended before the enumerator reaches the end is returned. When the enumerator is already at the end, it stops. It does not wait for a later append. A new resolution sees the collection as it is at that later call.

Elements are returned as they were stored. The collection read does not interpolate them, cast them, or rebuild them from decomposed leaves. Recomposition is out of scope. A typed consumer casts at binding time. `ResolveCollection` materializes a virtual collection into the property's element type while the module is prepared, so a mismatch fails in the existing validation pipeline. `Foreach` resolves `IEnumerable<object>` and binds each element into the iteration environment; the iteration body then validates whatever it reads. Append does not check element types, and a virtual collection does not require one CLR element type.

Exact indirection (`${myCollection[]}` or `${myCollection[+]}` as the entire stored string) preserves the collection object. The same expressions inside a larger string follow ordinary interpolation and use the resolved value's text form.

## Storage and visibility

Element identity and the empty-definition marker are stored beside ordinary variables, under keys that are not valid variable references. Enumerating an environment does not yield those keys. A present virtual collection is visible as one `name[]` entry whose value is a snapshot of the elements in that environment. Copying an environment through enumeration and `SetVariable`, including artifact publication, replays that entry as a definition. Adoption of an environment into a transaction uses the same visible entries, so the new transaction receives definitions rather than storage keys.

Inheritance follows ordinary shadowing. A read uses the nearest environment that has the collection. An append or definition in a child environment creates or replaces the child's collection and hides the parent collection of the same name. It does not extend the parent. Branches that should add to one collection select the same logical environment (`current`, `parent`, `global`, or one named environment).

Publishing a decomposable value whose root is already a collection assignment stores that value as one element and does not walk its leaves. `LeavesOnly`, `Shallow`, and `FullHierarchy` all mean the same thing on that root. Publishing any other root is unchanged, including when a leaf happens to be a CLR collection.

## Transactions

An append allocates a new binding. The binding key contains an order scope from the writing transaction plus a slot in that scope, so two appends never share a logical key.

The scope is a path of fixed-width slots:

- each transaction has one counter;
- an append and a nested fork each take the next slot;
- a fork reserves one slot on the owner, then gives the continuation branch `0` and each child the next index in creation order;
- a nested transaction extends its own prefix.

Fixed width keeps the path order equal to causal order. Elements written before a fork sort before that fork's elements. Elements of an earlier sequential child sort before a later child's. Inside one fork, the continuation sorts before children, and children sort in the order they were started. Parallel appends to one collection therefore merge with the existing conflict rule instead of requiring a special merge. Two definitions of the same collection still conflict, because the presence marker is one shared binding. A definition in one branch and appends in another do not conflict; the joined view contains the marker and every appended element.

Rollback publishes the fork baseline for workflow data, so a rolled-back branch contributes none of its new element bindings. Commit publishes them in the order above. The collection code does not have its own commit path.

Mutable environments that are not bound to a transaction, such as an artifact collection before it is published, use the same element representation and a local counter. Publication replays the visible snapshot into the target environment, which allocates that environment's own order scopes.

## Compatibility

`SetVariable`, `TryResolveVariable`, `TryRemoveVariable`, enumeration, and `Publish` keep their signatures. CLR collections assigned to ordinary names still resolve as those objects. Virtual collections are opt-in by name. Module workers that already iterate `IEnumerable<object>` or bind `IReadOnlyCollection<T>` do not branch on how the collection was assembled.
