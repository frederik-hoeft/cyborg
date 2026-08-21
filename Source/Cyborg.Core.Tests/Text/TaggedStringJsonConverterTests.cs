using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Text;
using System.Text.Json;

namespace Cyborg.Core.Tests.Text;

[TestClass]
public sealed class TaggedStringJsonConverterTests
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new TaggedStringJsonConverter() }
    };

    [TestMethod]
    public void Read_StringToken_IsUntagged()
    {
        TaggedString tagged = JsonSerializer.Deserialize<TaggedString>("\"plain\"", s_options);

        Assert.AreEqual("plain", tagged.Value);
        Assert.IsFalse(tagged.HasTags);
    }

    [TestMethod]
    public void Read_StructuralObject_PreservesTags()
    {
        const string JSON = """{ "value": "payload", "tags": ["cyborg.secret.v1", "custom"] }""";

        TaggedString tagged = JsonSerializer.Deserialize<TaggedString>(JSON, s_options);

        Assert.AreEqual("payload", tagged.Value);
        Assert.IsTrue(tagged.HasTag(WellKnownTags.SECRET));
        Assert.IsTrue(tagged.HasTag("custom"));
    }

    [TestMethod]
    public void Write_Untagged_IsBareString()
    {
        string json = JsonSerializer.Serialize((TaggedString)"plain", s_options);

        Assert.AreEqual("\"plain\"", json);
    }

    [TestMethod]
    public void Write_Tagged_IsStructuralObject()
    {
        TaggedString tagged = new("payload", [WellKnownTags.SECRET]);

        string json = JsonSerializer.Serialize(tagged, s_options);
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.AreEqual("payload", document.RootElement.GetProperty("value").GetString());
        Assert.AreEqual(WellKnownTags.SECRET, document.RootElement.GetProperty("tags")[0].GetString());
    }

    [TestMethod]
    public void Read_MissingValue_ThrowsJsonException()
    {
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<TaggedString>("""{ "tags": [] }""", s_options));
    }
}
