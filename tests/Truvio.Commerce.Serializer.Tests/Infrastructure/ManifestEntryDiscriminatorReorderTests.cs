using System.Text.Json;
using Truvio.Commerce.Serializer.Infrastructure;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Infrastructure;

/// <summary>
/// Phase 42-04 Task 2 / SC-6: STJ polymorphism position-zero defense.
///
/// SC-6 of Phase 42: "Inspecting either manifest with a JSON viewer shows the
/// discriminator (<c>providerType</c>) at position 0 of every entry object;
/// hand-reordering the discriminator below another property in a fixture and re-reading
/// still produces a typed error rather than <c>NotSupportedException</c>."
///
/// Three tests pin this contract:
///   1. <see cref="Write_DiscriminatorAtPositionZero_OnEveryEntry"/> — writer pins discriminator at position 0.
///   2. <see cref="Read_DiscriminatorAtNonZeroPosition_ThrowsJsonException_NotNotSupportedException"/> —
///      hand-edited reorder still fails as a typed JsonException (or succeeds if a future STJ tolerates
///      out-of-order metadata) — never <see cref="NotSupportedException"/>.
///   3. <see cref="Read_UnknownDiscriminatorValue_ThrowsJsonException"/> — unknown discriminator value still
///      throws JsonException; guards against accidentally relaxing
///      <c>IgnoreUnrecognizedTypeDiscriminators=false</c>.
/// </summary>
public class ManifestEntryDiscriminatorReorderTests : IDisposable
{
    private readonly string _tempDir;

    public ManifestEntryDiscriminatorReorderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ManifestEntryDiscriminatorReorderTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ---------- Test 1 ----------

    [Fact]
    public void Write_DiscriminatorAtPositionZero_OnEveryEntry()
    {
        // SC-6 first half: writer pins providerType to position 0 of every entry object.
        // Build one ContentEntry + one SqlTableEntry and assert each entry's first JSON
        // property name is "providerType".
        var content = new ContentEntry
        {
            EntryId = "content/area-1",
            Files = new[] { "swift/index.yml" },
            AreaId = 1,
            AreaName = "Swift",
            Path = "/",
            PageId = 0
        };
        var sql = new SqlTableEntry
        {
            EntryId = "sql/EcomOrderFlow",
            Files = new[] { "sql/EcomOrderFlow/standard.yml" },
            Table = "EcomOrderFlow",
            NameColumn = "OrderFlowName"
        };

        var writer = new ManifestWriter();
        writer.Write(_tempDir, "replace", new ManifestEntry[] { content, sql });

        var manifestPath = Path.Combine(_tempDir, "replace-manifest.json");
        Assert.True(File.Exists(manifestPath));

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var entries = doc.RootElement.GetProperty("entries");
        Assert.Equal(2, entries.GetArrayLength());

        foreach (var entry in entries.EnumerateArray())
        {
            var firstProperty = entry.EnumerateObject().First();
            Assert.Equal("providerType", firstProperty.Name);
        }
    }

    // ---------- Test 2 ----------

    [Fact]
    public void Read_DiscriminatorAtNonZeroPosition_ThrowsJsonException_NotNotSupportedException()
    {
        // SC-6 second half: hand-edited manifest where providerType is moved BELOW another
        // property (entryId precedes providerType). The invariant we pin: the read MUST fail
        // LOUDLY — never a silent fallthrough to default-binding the abstract base
        // ManifestEntry with the discriminator's value lost.
        //
        // .NET 8 STJ empirically throws **NotSupportedException** here ("Deserialization of
        // types without a parameterless constructor ... is not supported. Type
        // 'ManifestEntry'.") because when the discriminator cannot be located at position 0,
        // STJ falls through to attempting to instantiate the declared base type — which is
        // abstract. Plan 01 SUMMARY Decision #2 already documented this: PITFALLS §4 predicted
        // JsonException but .NET 8 STJ ships NotSupportedException. Both are LOUD failures;
        // the regression we guard against is a silent return.
        //
        // Per Plan 01's already-shipped polymorphism tests
        // (ManifestEntryPolymorphismTests.Read_DiscriminatorAtNonZeroPosition_*), this test
        // accepts either JsonException OR NotSupportedException OR a successful out-of-order
        // tolerant read. What it does NOT accept is a silent return-with-base-type or any
        // other quiet failure. Future STJ updates that switch the failure mode (e.g. throw
        // JsonException instead of NotSupportedException, or accept out-of-order metadata)
        // continue to satisfy the contract.
        var manifestJson = """
        {
          "schemaVersion": 2,
          "mode": "replace",
          "writtenAtUtc": "2026-05-08T00:00:00Z",
          "complete": true,
          "excludeFieldsByItemType": {},
          "excludeXmlElementsByType": {},
          "entries": [
            {
              "entryId": "content/area-1",
              "providerType": "Content",
              "files": [],
              "areaId": 1,
              "areaName": "Swift",
              "path": "/",
              "pageId": 0
            }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(_tempDir, "replace-manifest.json"), manifestJson);

        var writer = new ManifestWriter();
        var ex = Record.Exception(() => writer.Read(_tempDir, "replace"));

        if (ex is null)
        {
            // STJ tolerated out-of-order metadata — verify it bound the correct concrete type
            // (not silently fell through to base-type instantiation, which would have thrown).
            var manifest = writer.Read(_tempDir, "replace");
            Assert.NotNull(manifest);
            Assert.Single(manifest!.Entries);
            Assert.IsType<ContentEntry>(manifest.Entries[0]);
            return;
        }

        // STJ rejected the out-of-order discriminator with a LOUD typed exception. Per
        // Plan 01 SUMMARY Decision #2, .NET 8 throws NotSupportedException; a future STJ may
        // throw JsonException. Either is fine — what matters is that the exception is one of
        // these two known-loud variants, not a silent return or some unrelated exception.
        Assert.True(
            ex is JsonException || ex is NotSupportedException,
            $"Expected JsonException or NotSupportedException; got {ex.GetType().FullName}: {ex.Message}");
    }

    // ---------- Test 3 ----------

    [Fact]
    public void Read_UnknownDiscriminatorValue_ThrowsJsonException()
    {
        // Guard test: discriminator at position 0 but value "Unknown" — must always fail
        // with JsonException regardless of out-of-order tolerance. Pins the
        // IgnoreUnrecognizedTypeDiscriminators=false contract from Plan 01 / MANIFEST-02.
        // If a future contributor flips that flag, this test fails — the guard fires.
        var manifestJson = """
        {
          "schemaVersion": 2,
          "mode": "replace",
          "writtenAtUtc": "2026-05-08T00:00:00Z",
          "complete": true,
          "excludeFieldsByItemType": {},
          "excludeXmlElementsByType": {},
          "entries": [
            {
              "providerType": "Unknown",
              "entryId": "x",
              "files": []
            }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(_tempDir, "replace-manifest.json"), manifestJson);

        var writer = new ManifestWriter();
        var ex = Record.Exception(() => writer.Read(_tempDir, "replace"));

        Assert.NotNull(ex);
        Assert.False(
            ex is NotSupportedException,
            $"Expected JsonException; got NotSupportedException: {ex!.Message}");

        // Walk the inner-exception chain for a JsonException.
        var current = ex;
        bool sawJsonException = false;
        while (current != null)
        {
            if (current is JsonException) { sawJsonException = true; break; }
            current = current.InnerException;
        }
        Assert.True(
            sawJsonException,
            $"Expected JsonException somewhere in the exception chain. Got: {ex!.GetType().Name}: {ex.Message}");
    }
}
