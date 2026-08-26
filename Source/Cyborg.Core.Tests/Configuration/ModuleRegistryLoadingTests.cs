using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.TestModules.Activation;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Cyborg.Core.Tests.Configuration;

[TestClass]
public sealed class ModuleRegistryLoadingTests
{
    [TestMethod]
    public void Deserialize_CollectsNamedModulesIntoImmutableRootSeed()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(new ModuleContextJsonConverter());
        options.Converters.Add(new TestModuleReferenceConverter());
        TestJsonLoaderContext context = new(options);
        ModuleConfigurationDeserializer deserializer = new(context);
        const string json = """
            {
              "module": {
                "kind": "container",
                "name": "outer",
                "child": {
                  "module": {
                    "kind": "leaf",
                    "name": "child"
                  },
                  "environment": {},
                  "requires": { "arguments": [] }
                }
              },
              "environment": {},
              "requires": { "arguments": [] }
            }
            """;

        ModuleContext root = deserializer.Deserialize(json)
            ?? throw new AssertFailedException("Expected a loaded module context.");
        Dictionary<string, ModuleContext> namedModules = root.NamedModules.Modules.ToDictionary(StringComparer.Ordinal);

        Assert.HasCount(2, namedModules);
        Assert.AreEqual("outer", namedModules["outer"].Module.Definition.Name);
        Assert.AreEqual("child", namedModules["child"].Module.Definition.Name);
    }

    private sealed class TestJsonLoaderContext(JsonSerializerOptions options) : IJsonLoaderContext
    {
        public IServiceProvider ServiceProvider => throw new NotSupportedException();

        public JsonSerializerOptions JsonSerializerOptions { get; } = options;
    }

    private sealed class TestModuleReferenceConverter : JsonConverter<ModuleReference>
    {
        public override ModuleReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            string kind = root.GetProperty("kind").GetString()!;
            string name = root.GetProperty("name").GetString()!;
            if (kind == "leaf")
            {
                ActivationProbeModule module = new() { Name = name };
                return new ModuleReference(module, ActivationProbeModule.ModuleId);
            }

            string childJson = root.GetProperty("child").GetRawText();
            ModuleContext child = JsonSerializer.Deserialize<ModuleContext>(childJson, options)
                ?? throw new JsonException("Failed to deserialize nested test module context.");
            TestContainerModule container = new(child) { Name = name };
            return new ModuleReference(container, TestContainerModule.ModuleId);
        }

        public override void Write(Utf8JsonWriter writer, ModuleReference value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }

    private sealed record TestContainerModule(ModuleContext Child) : ModuleBase, IModuleDefinition
    {
        public static string ModuleId => "cyborg.core.tests.container.v1";
    }
}
