using Cyborg.Core.Runtime.Engine.Transactions.Collections;

namespace Cyborg.Core.Tests.Runtime.Transactions;

[TestClass]
public sealed class TransactionalDictionaryTests
{
    [TestMethod]
    public void Set_BaselineValue_RetainsChangeProvenance()
    {
        TransactionalDictionary<string, int> dictionary = Create(("value", 1));

        dictionary.Set("value", 1);

        Assert.AreEqual(1, dictionary["value"]);
        Assert.AreEqual(1, dictionary.ChangeCount);
        Assert.IsTrue(dictionary.TryGetChange("value", out TransactionalDictionaryChange<int> change));
        Assert.AreEqual(TransactionalDictionaryChangeKind.Set, change.Kind);
        Assert.AreEqual(1, change.Value);
    }


    [TestMethod]
    public void Set_NullValue_IsDistinctFromRemoval()
    {
        TransactionalDictionary<string, object?> dictionary = new(StringComparer.Ordinal);

        dictionary.Set("value", null);

        Assert.IsTrue(dictionary.TryGetValue("value", out object? value));
        Assert.IsNull(value);
        Assert.IsTrue(dictionary.TryGetChange("value", out TransactionalDictionaryChange<object?> change));
        Assert.AreEqual(TransactionalDictionaryChangeKind.Set, change.Kind);
        Assert.IsNull(change.Value);
    }

    [TestMethod]
    public void TryRemove_AddedValue_RetainsNegativeChangeWhenEffectiveStateMatchesBaseline()
    {
        TransactionalDictionary<string, int> dictionary = new(StringComparer.Ordinal);
        dictionary.Set("value", 1);

        bool removed = dictionary.TryRemove("value");

        Assert.IsTrue(removed);
        Assert.IsFalse(dictionary.ContainsKey("value"));
        Assert.AreEqual(0, dictionary.Count);
        Assert.AreEqual(1, dictionary.ChangeCount);
        Assert.IsTrue(dictionary.TryGetChange("value", out TransactionalDictionaryChange<int> change));
        Assert.AreEqual(TransactionalDictionaryChangeKind.Remove, change.Kind);
    }

    [TestMethod]
    public void TryRemove_AbsentValue_DoesNotCreateChange()
    {
        TransactionalDictionary<string, int> dictionary = new(StringComparer.Ordinal);

        bool removed = dictionary.TryRemove("missing");

        Assert.IsFalse(removed);
        Assert.AreEqual(0, dictionary.ChangeCount);
    }

    [TestMethod]
    public void Removal_ShadowsBaselineUntilSetAgain()
    {
        TransactionalDictionary<string, int> dictionary = Create(("value", 1));

        Assert.IsTrue(dictionary.TryRemove("value"));
        Assert.IsFalse(dictionary.TryGetValue("value", out int _));
        Assert.IsTrue(dictionary.TryAdd("value", 2));

        Assert.AreEqual(2, dictionary["value"]);
        Assert.AreEqual(1, dictionary.ChangeCount);
        Assert.IsTrue(dictionary.TryGetChange("value", out TransactionalDictionaryChange<int> change));
        Assert.AreEqual(TransactionalDictionaryChangeKind.Set, change.Kind);
        Assert.AreEqual(2, change.Value);
    }

    [TestMethod]
    public void Freeze_ReturnsStableImmutableSnapshot()
    {
        TransactionalDictionary<string, int> dictionary = Create(("baseline", 1));
        dictionary.Set("first", 2);
        TransactionalDictionarySnapshot<string, int> firstSnapshot = dictionary.Freeze();

        dictionary.Set("second", 3);
        TransactionalDictionarySnapshot<string, int> secondSnapshot = dictionary.Freeze();

        Assert.AreNotSame(firstSnapshot, secondSnapshot);
        Assert.AreEqual(2, firstSnapshot.Count);
        Assert.IsFalse(firstSnapshot.ContainsKey("second"));
        Assert.AreEqual(3, secondSnapshot.Count);
        Assert.AreEqual(3, secondSnapshot["second"]);
    }

    [TestMethod]
    public void Fork_MultipleBranchesShareExactBaselineAndRemainIsolated()
    {
        TransactionalDictionary<string, int> owner = Create(("baseline", 1));
        owner.Set("before-fork", 2);
        TransactionalDictionarySnapshot<string, int> forkBaseline = owner.Freeze();

        TransactionalDictionary<string, int> first = owner.Fork();
        TransactionalDictionary<string, int> second = owner.Fork();
        first.Set("branch", 10);
        second.Set("branch", 20);

        Assert.AreSame(forkBaseline, first.Baseline);
        Assert.AreSame(forkBaseline, second.Baseline);
        Assert.AreEqual(10, first["branch"]);
        Assert.AreEqual(20, second["branch"]);
        Assert.IsFalse(owner.ContainsKey("branch"));
    }

    [TestMethod]
    public void TryPrepareMerge_NonOverlappingBranchesProducesCandidateWithoutMutatingOwner()
    {
        TransactionalDictionary<string, int> owner = Create(("baseline", 1));
        owner.Set("owner", 2);
        TransactionalDictionarySnapshot<string, int> forkBaseline = owner.Freeze();
        TransactionalDictionary<string, int> first = owner.Fork();
        TransactionalDictionary<string, int> second = owner.Fork();
        first.Set("first", 3);
        second.Set("second", 4);

        bool merged = owner.TryPrepareMerge(
            forkBaseline,
            [first, second],
            out TransactionalDictionary<string, int>? candidate,
            out string conflictKey);

        Assert.IsTrue(merged, conflictKey);
        Assert.IsNotNull(candidate);
        Assert.IsFalse(owner.ContainsKey("first"));
        Assert.IsFalse(owner.ContainsKey("second"));
        Assert.AreEqual(2, owner["owner"]);
        Assert.AreEqual(2, candidate["owner"]);
        Assert.AreEqual(3, candidate["first"]);
        Assert.AreEqual(4, candidate["second"]);
        Assert.AreEqual(3, candidate.ChangeCount);
    }

    [TestMethod]
    public void TryPrepareMerge_SameKeySetConflictsEvenWhenValuesAreEqual()
    {
        TransactionalDictionary<string, int> owner = new(StringComparer.Ordinal);
        TransactionalDictionarySnapshot<string, int> forkBaseline = owner.Freeze();
        TransactionalDictionary<string, int> first = owner.Fork();
        TransactionalDictionary<string, int> second = owner.Fork();
        first.Set("first-only", 2);
        first.Set("value", 1);
        second.Set("value", 1);

        bool merged = owner.TryPrepareMerge(
            forkBaseline,
            [first, second],
            out TransactionalDictionary<string, int>? candidate,
            out string conflictKey);

        Assert.IsFalse(merged);
        Assert.IsNull(candidate);
        Assert.AreEqual("value", conflictKey);
        Assert.IsFalse(owner.ContainsKey("first-only"));
        Assert.IsFalse(owner.ContainsKey("value"));
    }

    [TestMethod]
    public void TryPrepareMerge_SetAndRemoveConflict()
    {
        TransactionalDictionary<string, int> owner = Create(("value", 1));
        TransactionalDictionarySnapshot<string, int> forkBaseline = owner.Freeze();
        TransactionalDictionary<string, int> first = owner.Fork();
        TransactionalDictionary<string, int> second = owner.Fork();
        first.Set("value", 2);
        Assert.IsTrue(second.TryRemove("value"));

        bool merged = owner.TryPrepareMerge(
            forkBaseline,
            [first, second],
            out TransactionalDictionary<string, int>? candidate,
            out string conflictKey);

        Assert.IsFalse(merged);
        Assert.IsNull(candidate);
        Assert.AreEqual("value", conflictKey);
        Assert.AreEqual(1, owner["value"]);
    }

    [TestMethod]
    public void TryPrepareMerge_RemoveAndRemoveConflict()
    {
        TransactionalDictionary<string, int> owner = Create(("value", 1));
        TransactionalDictionarySnapshot<string, int> forkBaseline = owner.Freeze();
        TransactionalDictionary<string, int> first = owner.Fork();
        TransactionalDictionary<string, int> second = owner.Fork();
        Assert.IsTrue(first.TryRemove("value"));
        Assert.IsTrue(second.TryRemove("value"));

        bool merged = owner.TryPrepareMerge(
            forkBaseline,
            [first, second],
            out TransactionalDictionary<string, int>? candidate,
            out string conflictKey);

        Assert.IsFalse(merged);
        Assert.IsNull(candidate);
        Assert.AreEqual("value", conflictKey);
        Assert.AreEqual(1, owner["value"]);
    }

    [TestMethod]
    public void TryPrepareMerge_ChildRemovalCanReplaceOwnerPreForkSetWithoutConflict()
    {
        TransactionalDictionary<string, int> owner = new(StringComparer.Ordinal);
        owner.Set("value", 1);
        TransactionalDictionarySnapshot<string, int> forkBaseline = owner.Freeze();
        TransactionalDictionary<string, int> child = owner.Fork();
        Assert.IsTrue(child.TryRemove("value"));

        bool merged = owner.TryPrepareMerge(
            forkBaseline,
            [child],
            out TransactionalDictionary<string, int>? candidate,
            out string conflictKey);

        Assert.IsTrue(merged, conflictKey);
        Assert.IsNotNull(candidate);
        Assert.IsFalse(candidate.ContainsKey("value"));
        Assert.IsTrue(candidate.TryGetChange("value", out TransactionalDictionaryChange<int> change));
        Assert.AreEqual(TransactionalDictionaryChangeKind.Remove, change.Kind);
    }

    [TestMethod]
    public void TryPrepareMerge_RepeatedForkGenerationsRetainEarlierProvenance()
    {
        TransactionalDictionary<string, int> root = new(StringComparer.Ordinal);
        TransactionalDictionarySnapshot<string, int> rootForkBaseline = root.Freeze();
        TransactionalDictionary<string, int> outer = root.Fork();
        outer.Set("outer", 1);

        TransactionalDictionarySnapshot<string, int> firstForkBaseline = outer.Freeze();
        TransactionalDictionary<string, int> firstChild = outer.Fork();
        firstChild.Set("first-child", 2);
        Assert.IsTrue(outer.TryPrepareMerge(
            firstForkBaseline,
            [firstChild],
            out TransactionalDictionary<string, int>? afterFirstChild,
            out string firstConflict));
        Assert.IsNotNull(afterFirstChild, firstConflict);

        TransactionalDictionarySnapshot<string, int> secondForkBaseline = afterFirstChild.Freeze();
        TransactionalDictionary<string, int> secondChild = afterFirstChild.Fork();
        secondChild.Set("second-child", 3);
        Assert.IsTrue(afterFirstChild.TryPrepareMerge(
            secondForkBaseline,
            [secondChild],
            out TransactionalDictionary<string, int>? completedOuter,
            out string secondConflict));
        Assert.IsNotNull(completedOuter, secondConflict);

        Assert.AreEqual(3, completedOuter.ChangeCount);
        Assert.IsTrue(root.TryPrepareMerge(
            rootForkBaseline,
            [completedOuter],
            out TransactionalDictionary<string, int>? completedRoot,
            out string rootConflict));
        Assert.IsNotNull(completedRoot, rootConflict);
        Assert.AreEqual(1, completedRoot["outer"]);
        Assert.AreEqual(2, completedRoot["first-child"]);
        Assert.AreEqual(3, completedRoot["second-child"]);
        Assert.AreEqual(3, completedRoot.ChangeCount);
    }

    [TestMethod]
    public void TryPrepareMerge_OwnerMutationAfterForkIsRejected()
    {
        TransactionalDictionary<string, int> owner = new(StringComparer.Ordinal);
        TransactionalDictionarySnapshot<string, int> forkBaseline = owner.Freeze();
        TransactionalDictionary<string, int> child = owner.Fork();
        owner.Set("post-fork", 1);

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            owner.TryPrepareMerge(
                forkBaseline,
                [child],
                out TransactionalDictionary<string, int>? _,
                out string _));

        StringAssert.Contains(exception.Message, "changed after the fork baseline");
    }

    [TestMethod]
    public void TryPrepareMerge_UnrelatedContributorBaselineIsRejected()
    {
        TransactionalDictionary<string, int> owner = new(StringComparer.Ordinal);
        TransactionalDictionarySnapshot<string, int> forkBaseline = owner.Freeze();
        TransactionalDictionary<string, int> unrelatedOwner = new(StringComparer.Ordinal);
        TransactionalDictionary<string, int> unrelated = unrelatedOwner.Fork();

        Assert.ThrowsExactly<ArgumentException>(() =>
            owner.TryPrepareMerge(
                forkBaseline,
                [unrelated],
                out TransactionalDictionary<string, int>? _,
                out string _));
    }

    [TestMethod]
    public void KeyComparer_IsPreservedAcrossForkAndMerge()
    {
        TransactionalDictionary<string, int> owner = new(StringComparer.OrdinalIgnoreCase);
        owner.Set("Value", 1);
        TransactionalDictionarySnapshot<string, int> forkBaseline = owner.Freeze();
        TransactionalDictionary<string, int> child = owner.Fork();

        Assert.AreEqual(1, child["value"]);
        child.Set("Other", 2);
        Assert.IsTrue(owner.TryPrepareMerge(
            forkBaseline,
            [child],
            out TransactionalDictionary<string, int>? candidate,
            out string conflictKey), conflictKey);
        Assert.IsNotNull(candidate);
        Assert.AreEqual(2, candidate["other"]);
    }

    private static TransactionalDictionary<string, int> Create(params (string Key, int Value)[] values)
    {
        KeyValuePair<string, int>[] pairs = [.. values.Select(static value => KeyValuePair.Create(value.Key, value.Value))];
        return pairs.ToTransactionalDictionary(StringComparer.Ordinal);
    }
}
