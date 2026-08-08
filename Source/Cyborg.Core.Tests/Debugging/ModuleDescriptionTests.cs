using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Descriptors.Builders;
using Cyborg.Core.Modules.Descriptors.Model;
using Cyborg.Core.Modules.Descriptors.Writers;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class ModuleDescriptionTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task Test_ToTextAsync_RendersNestedObjectsAndCollectionsAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        TestDescriptor descriptor = new();

        string result = await serializationService.ToTextAsync(descriptor, TestContext.CancellationToken);

        Assert.Contains("ModuleId: \"cyborg.tests.description.v1\"", result);
        Assert.Contains("Options:", result);
        Assert.Contains("  Enabled: true", result);
        Assert.Contains("Items:", result);
        Assert.Contains("  [0]: \"first\"", result);
        Assert.Contains("  [1]:", result);
        Assert.Contains("    Value: 42", result);
    });

    [TestMethod]
    public Task Test_ToJsonAsync_RendersValidNestedJsonAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        TestDescriptor descriptor = new();

        string result = await serializationService.ToJsonAsync(descriptor, TestContext.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(result);

        JsonElement root = document.RootElement;
        Assert.AreEqual(
            "cyborg.tests.description.v1",
            root.GetProperty("ModuleId").GetString());
        Assert.IsTrue(root.GetProperty("Options").GetProperty("Enabled").GetBoolean());
        Assert.AreEqual("first", root.GetProperty("Items")[0].GetString());
        Assert.AreEqual(42, root.GetProperty("Items")[1].GetProperty("Value").GetInt32());
    });

    [TestMethod]
    public Task Test_BuildAsync_PreservesArbitraryHintsAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        HintDescriptor descriptor = new();

        IDescriptionObjectComponent result = await serializationService.BuildAsync(descriptor, TestContext.CancellationToken);

        Assert.HasCount(1, result.Properties);
        IDescriptionPropertyComponent property = result.Properties[0];
        Assert.AreEqual("Password", property.Name);
        Assert.AreSequenceEqual(["secret", "application-specific"], property.Hints);
    });

    [TestMethod]
    public Task Test_SerializeAsync_CustomSerializerCanInterpretHintsAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        HintDescriptor descriptor = new();
        RedactingDescriptionSerializer serializer = new();

        string result = await serializationService.SerializeAsync(descriptor, serializer, TestContext.CancellationToken);

        Assert.AreEqual("Password=<redacted>", result);
    });

    [TestMethod]
    public Task Test_ToTextAsync_EscapesScalarControlCharactersAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        EscapingDescriptor descriptor = new();

        string result = await serializationService.ToTextAsync(descriptor, TestContext.CancellationToken);

        Assert.Contains("Text: \"line\\n\\t\\\\\\\"\\0\"", result);
        Assert.Contains("Quote: '\\''", result);
    });

    [TestMethod]
    public Task Test_ToTextAsync_FormatsScalarsDeterministicallyAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        ScalarDescriptor descriptor = new();

        string result = await serializationService.ToTextAsync(descriptor, TestContext.CancellationToken);

        Assert.Contains("Null: null", result);
        Assert.Contains("Decimal: 12.5", result);
        Assert.Contains("DateTime: 2026-01-02T03:04:05.0000000Z", result);
        Assert.Contains("DateTimeOffset: 2026-01-02T03:04:05.0000000+02:00", result);
        Assert.Contains("Duration: 01:02:03", result);
        Assert.Contains("Guid: 01234567-89ab-cdef-0123-456789abcdef", result);
        Assert.Contains("Enum: DayOfWeek.Monday", result);
    });

    [TestMethod]
    public Task Test_SerializeAsync_PropagatesCancellationTokenAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();
        CancellationTrackingDescriptor descriptor = new();
        CancellationTrackingSerializer serializer = new();
        using CancellationTokenSource cancellationSource = new();

        await serializationService.SerializeAsync(descriptor, serializer, cancellationSource.Token);

        Assert.AreEqual(cancellationSource.Token, descriptor.CancellationToken);
        Assert.AreEqual(cancellationSource.Token, serializer.CancellationToken);
    });

    [TestMethod]
    public Task Test_SerializeAsync_CustomSerializerRegisteredThroughDi_IsResolvedByFormatAsync() => TestWithDIAsync(async services =>
    {
        IModuleSerializationService serializationService = services.GetRequiredService<IModuleSerializationService>();

        string result = await serializationService.SerializeAsync(new TestDescriptor(), "application/x-cyborg-test", TestContext.CancellationToken);

        Assert.AreEqual("custom-from-di", result);
    }, static services => services.AddSingleton<IModuleDescriptionSerializer, CustomSerializer>());

    [TestMethod]
    public void Test_SerializerRegistry_ResolvesCustomFormat()
    {
        CustomDescriptionSerializer serializer = new();
        DefaultModuleDescriptionSerializerRegistry registry = new([serializer]);

        IModuleDescriptionSerializer result = registry.GetRequiredSerializer("CUSTOM");

        Assert.AreSame(serializer, result);
        Assert.IsTrue(registry.TryGetSerializer("custom", out IModuleDescriptionSerializer? found));
        Assert.AreSame(serializer, found);
    }

    [TestMethod]
    public void Test_SerializerRegistry_DuplicateFormat_Throws()
    {
        CustomDescriptionSerializer first = new();
        CustomDescriptionSerializer second = new();

        Assert.Throws<InvalidOperationException>(
            () => new DefaultModuleDescriptionSerializerRegistry([first, second]));
    }

    private sealed class TestDescriptor : IModuleDescriptor
    {
        public ValueTask DescribeAsync(IObjectDescriptionBuilder builder, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AddProperty("ModuleId", [], "cyborg.tests.description.v1");
            builder.AddObject("Options", [], static options => options.AddProperty("Enabled", [], true));
            builder.AddCollection("Items", [], static items =>
            {
                items.AddItem([], "first");
                items.AddObjectItem([], static item => item.AddProperty("Value", [], 42));
            });
            return ValueTask.CompletedTask;
        }
    }

    private sealed class HintDescriptor : IModuleDescriptor
    {
        public ValueTask DescribeAsync(IObjectDescriptionBuilder builder, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AddProperty("Password", ["secret", "application-specific"], "sensitive");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class EscapingDescriptor : IModuleDescriptor
    {
        public ValueTask DescribeAsync(IObjectDescriptionBuilder builder, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AddProperty("Text", [], "line\n\t\\\"\0");
            builder.AddProperty("Quote", [], '\'');
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScalarDescriptor : IModuleDescriptor
    {
        public ValueTask DescribeAsync(IObjectDescriptionBuilder builder, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AddProperty("Null", [], (string?)null);
            builder.AddProperty("Decimal", [], 12.5m);
            builder.AddProperty("DateTime", [], new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            builder.AddProperty("DateTimeOffset", [], new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)));
            builder.AddProperty("Duration", [], new TimeSpan(1, 2, 3));
            builder.AddProperty("Guid", [], Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
            builder.AddProperty("Enum", [], DayOfWeek.Monday);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RedactingDescriptionSerializer : IModuleDescriptionSerializer, IDescriptionComponentWriter
    {
        private const string SECRET_HINT = "secret";

        private readonly StringBuilder _builder = new();

        public string Format => "redacted";

        public async ValueTask<string> SerializeAsync(IDescriptionObjectComponent description, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(description);
            _builder.Clear();
            await description.AcceptAsync(this, cancellationToken).ConfigureAwait(false);
            return _builder.ToString();
        }

        public ValueTask WriteAtomAsync<T>(T value, ImmutableArray<string> hints, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _builder.Append(value);
            return ValueTask.CompletedTask;
        }

        public async ValueTask WriteAsync(IDescriptionObjectComponent objectComponent, CancellationToken cancellationToken)
        {
            foreach (IDescriptionPropertyComponent property in objectComponent.Properties)
            {
                await property.AcceptAsync(this, cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask WriteAsync(IDescriptionCollectionComponent collectionComponent, CancellationToken cancellationToken)
        {
            foreach (IDescriptionValueComponent item in collectionComponent.Items)
            {
                await item.AcceptAsync(this, cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask WriteAsync(IDescriptionPropertyComponent propertyComponent, CancellationToken cancellationToken)
        {
            _builder.Append(propertyComponent.Name).Append('=');
            foreach (string hint in propertyComponent.Hints)
            {
                if (hint == SECRET_HINT)
                {
                    _builder.Append("<redacted>");
                    return;
                }
            }

            await propertyComponent.Value.AcceptAsync(this, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class CancellationTrackingDescriptor : IModuleDescriptor
    {
        public CancellationToken CancellationToken { get; private set; }

        public ValueTask DescribeAsync(IObjectDescriptionBuilder builder, CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            builder.AddProperty("Value", [], 1);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CancellationTrackingSerializer : IModuleDescriptionSerializer
    {
        public string Format => "tracking";

        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<string> SerializeAsync(IDescriptionObjectComponent description, CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return ValueTask.FromResult("tracked");
        }
    }

    private sealed class CustomDescriptionSerializer : IModuleDescriptionSerializer
    {
        public string Format => "custom";

        public ValueTask<string> SerializeAsync(IDescriptionObjectComponent description, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(description);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult("custom");
        }
    }

    private sealed class CustomSerializer : IModuleDescriptionSerializer
    {
        public string Format => "application/x-cyborg-test";

        public ValueTask<string> SerializeAsync(IDescriptionObjectComponent description, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(description);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult("custom-from-di");
        }
    }
}
