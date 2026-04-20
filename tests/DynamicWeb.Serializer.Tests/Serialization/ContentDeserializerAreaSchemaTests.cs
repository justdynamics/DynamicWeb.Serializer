using System.Reflection;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Serialization;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Serialization;

/// <summary>
/// Phase 37-02: ContentDeserializer's Area schema-tolerance path now delegates to the shared
/// TargetSchemaCache. These tests verify the wiring at the class level — constructor accepts
/// the cache, legacy private helpers are gone, and the field is threaded through.
/// Behavioural regression coverage lives in the existing integration tests
/// (CustomerCenterDeserializationTests) and the SqlTableProviderCoercionTests contract test.
/// </summary>
[Trait("Category", "Phase37-02")]
public class ContentDeserializerAreaSchemaTests
{
    private static SerializerConfiguration MinimalConfig() => new()
    {
        OutputDirectory = Path.GetTempPath()
    };

    // -------------------------------------------------------------------------
    // Constructor accepts a TargetSchemaCache
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_AcceptsTargetSchemaCache()
    {
        var cache = new TargetSchemaCache(_ =>
            (new HashSet<string>(StringComparer.OrdinalIgnoreCase),
             new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));

        // Must compile & run — confirms the new optional parameter exists on the ctor.
        var deserializer = new ContentDeserializer(
            MinimalConfig(),
            schemaCache: cache);

        Assert.NotNull(deserializer);
    }

    [Fact]
    public void Constructor_WithoutCache_CreatesDefaultInstance()
    {
        // Backwards-compatible: existing call sites (ContentProvider, commands, integration tests)
        // keep working without explicitly passing a cache.
        var deserializer = new ContentDeserializer(MinimalConfig());
        Assert.NotNull(deserializer);
    }

    // -------------------------------------------------------------------------
    // Structural: the _schemaCache field exists and legacy fields are gone
    // -------------------------------------------------------------------------

    [Fact]
    public void LegacyAreaSchemaFields_AreRemoved()
    {
        var t = typeof(ContentDeserializer);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Assert.Null(t.GetField("_targetAreaColumns", flags));
        Assert.Null(t.GetField("_targetAreaColumnTypes", flags));
        Assert.Null(t.GetField("_loggedAreaColumnMissing", flags));
    }

    [Fact]
    public void LegacyAreaSchemaMethods_AreRemoved()
    {
        var t = typeof(ContentDeserializer);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Assert.Null(t.GetMethod("GetTargetAreaColumns", flags));
        Assert.Null(t.GetMethod("EnsureTargetAreaSchema", flags));
        Assert.Null(t.GetMethod("CoerceForColumn", flags));
    }

    [Fact]
    public void SchemaCacheField_IsPresent()
    {
        var t = typeof(ContentDeserializer);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = t.GetField("_schemaCache", flags);
        Assert.NotNull(field);
        Assert.Equal(typeof(TargetSchemaCache), field!.FieldType);
    }

    [Fact]
    public void InjectedCache_IsStoredOnInstance()
    {
        var cache = new TargetSchemaCache(_ =>
            (new HashSet<string>(StringComparer.OrdinalIgnoreCase),
             new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
        var deserializer = new ContentDeserializer(MinimalConfig(), schemaCache: cache);

        var field = typeof(ContentDeserializer).GetField(
            "_schemaCache", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var stored = field!.GetValue(deserializer);
        Assert.Same(cache, stored);
    }
}
