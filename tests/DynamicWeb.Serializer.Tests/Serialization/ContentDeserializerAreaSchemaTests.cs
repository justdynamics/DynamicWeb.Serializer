using System.Reflection;
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
///
/// <para>
/// Phase 44 / D-04 + BLOCKER 1: constructor input pivoted from
/// <c>SerializerConfiguration</c> to <c>(ContentEntry entry, string contentRoot, ...)</c>.
/// The constructor-wiring tests below were updated to the new shape; the reflection-based
/// invariants further down still target <c>typeof(ContentDeserializer)</c> unchanged.
/// </para>
/// </summary>
[Trait("Category", "Phase37-02")]
public class ContentDeserializerAreaSchemaTests
{
    private static ContentEntry StubContentEntry() => new()
    {
        EntryId = "test/stub",
        Files = Array.Empty<string>(),
        AreaId = 1,
        AreaName = "Test",
        Path = "/",
        PageId = 0
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

        // Must compile & run — confirms the optional schemaCache parameter exists on the new ctor.
        var deserializer = new ContentDeserializer(
            StubContentEntry(),
            Path.GetTempPath(),
            schemaCache: cache);

        Assert.NotNull(deserializer);
    }

    [Fact]
    public void Constructor_WithoutCache_CreatesDefaultInstance()
    {
        // Phase 44 / D-04: covers the new constructor's default-cache fallback. Existing call
        // sites (ContentProvider, commands, integration tests) keep working without explicitly
        // passing a cache because the new ctor still has the optional schemaCache parameter.
        var deserializer = new ContentDeserializer(StubContentEntry(), Path.GetTempPath());
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
        var deserializer = new ContentDeserializer(StubContentEntry(), Path.GetTempPath(), schemaCache: cache);

        var field = typeof(ContentDeserializer).GetField(
            "_schemaCache", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var stored = field!.GetValue(deserializer);
        Assert.Same(cache, stored);
    }
}
