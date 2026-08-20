using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;
using System.Collections.Immutable;

namespace Cyborg.Core.Tests.Text;

[TestClass]
public sealed class TaggedStringTests
{
    [TestMethod]
    public void ImplicitConversion_FromString_HasNoTags()
    {
        TaggedString tagged = "hello";

        Assert.AreEqual("hello", tagged.Value);
        Assert.IsFalse(tagged.HasTags);
        Assert.IsFalse(tagged.HasTag(WellKnownTags.Secret));
    }

    [TestMethod]
    public void ImplicitConversion_ToString_ReturnsRawValue()
    {
        TaggedString tagged = new("secret-value", [WellKnownTags.Secret]);

        string raw = (string)tagged;

        Assert.AreEqual("secret-value", raw);
    }

    [TestMethod]
    public void ToString_SecretTag_IsRedacted()
    {
        TaggedString tagged = new("secret-value", [WellKnownTags.Secret]);

        Assert.AreEqual(SecretTagHandler.RedactedDisplay, tagged.ToString());
    }

    [TestMethod]
    public void ToString_Untagged_ReturnsValue()
    {
        TaggedString tagged = new("visible");

        Assert.AreEqual("visible", tagged.ToString());
    }

    [TestMethod]
    public void WithTag_UnionsAndDeduplicates()
    {
        TaggedString tagged = new("value", ["alpha"]);

        TaggedString withSecret = tagged.WithTag(WellKnownTags.Secret).WithTag("alpha");

        Assert.AreEqual("value", withSecret.Value);
        Assert.IsTrue(withSecret.HasTag("alpha"));
        Assert.IsTrue(withSecret.HasTag(WellKnownTags.Secret));
        Assert.HasCount(2, withSecret.Tags);
    }

    [TestMethod]
    public void Concat_UnionsTagsAndJoinsValues()
    {
        TaggedString left = new("hello ", ["greeting"]);
        TaggedString right = new("world", [WellKnownTags.Secret]);

        TaggedString combined = TaggedString.Concat(left, right);

        Assert.AreEqual("hello world", combined.Value);
        Assert.IsTrue(combined.HasTag("greeting"));
        Assert.IsTrue(combined.HasTag(WellKnownTags.Secret));
        Assert.AreEqual(SecretTagHandler.RedactedDisplay, combined.ToString());
    }

    [TestMethod]
    public void Equals_ComparesValueAndTags()
    {
        TaggedString left = new("same", ["a", "b"]);
        TaggedString right = new("same", ["b", "a"]);
        TaggedString differentTags = new("same", ["a"]);
        TaggedString differentValue = new("other", ["a", "b"]);

        Assert.AreEqual(left, right);
        Assert.AreNotEqual(left, differentTags);
        Assert.AreNotEqual(left, differentValue);
        Assert.IsTrue(left.Equals("same"));
    }

    [TestMethod]
    public void Constructor_RejectsEmptyTags()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new TaggedString("value", new[] { string.Empty }));
    }

    [TestMethod]
    public void Empty_DefaultInstance_HasEmptyValueAndNoTags()
    {
        TaggedString tagged = default;

        Assert.AreEqual(string.Empty, tagged.Value);
        Assert.IsFalse(tagged.HasTags);
        Assert.IsTrue(tagged.IsEmpty);
        CollectionAssert.AreEqual(ImmutableHashSet<string>.Empty.ToArray(), tagged.Tags.ToArray());
    }

    [TestMethod]
    public void Renderer_HandlerAfterSecret_CannotRecoverRawValue()
    {
        DefaultTaggedStringRenderer renderer = new([new SecretTagHandler(), new DecoratingTagHandler()]);
        TaggedString tagged = new("secret-value", [WellKnownTags.Secret, DecoratingTagHandler.TagName]);

        string rendered = renderer.Render(tagged);

        Assert.AreEqual($"decorated({SecretTagHandler.RedactedDisplay})", rendered);
        Assert.DoesNotContain("secret-value", rendered);
    }

    [TestMethod]
    public void Equals_Object_DoesNotClaimCrossTypeEquality()
    {
        TaggedString tagged = new("same");
        object raw = "same";

        Assert.IsFalse(tagged.Equals(raw));
        Assert.IsFalse(raw.Equals(tagged));
        Assert.IsTrue(tagged.Equals("same"));
    }

    private sealed class DecoratingTagHandler : ITaggedStringTagHandler
    {
        public const string TagName = "zzz.test.decorate";

        public string Tag => TagName;

        public string Render(string current) => $"decorated({current})";
    }
}
