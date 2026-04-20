using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Phase 37-05 / TEMPLATE-01 (D-19, D-20): manifest-only template tracking. Records
/// which cshtml / grid-row / item-type files the baseline references and validates
/// their presence at deserialize. Does NOT serialize template file contents — the
/// templates ship alongside the code (per D-19) and the manifest is a list of
/// expected assets plus the source pages that reference each one.
/// </summary>
public record TemplateReference
{
    public string Path { get; init; } = string.Empty;
    /// <summary>One of: "page-layout", "grid-row", "item-type".</summary>
    public string Kind { get; init; } = string.Empty;
    /// <summary>Source page identifiers (path or GUID) that trigger this reference.</summary>
    public List<string> ReferencedBy { get; init; } = new();
}

/// <summary>
/// Reads / writes / validates a <c>templates.manifest.yml</c> file alongside the
/// serialized baseline. See <see cref="TemplateReference"/> for shape.
/// </summary>
public class TemplateAssetManifest
{
    public const string ManifestFileName = "templates.manifest.yml";

    // T-37-05-05 DoS guard: a malicious manifest claiming millions of references would
    // force YamlDotNet to allocate unbounded memory. Cap at 100k — well above any
    // realistic baseline (Swift 2.2's ~1500 pages yields ~10 refs).
    public const int MaxReferences = 100_000;

    // T-37-05-01 path-traversal guard. Allow letters, digits, underscore, hyphen, dot,
    // and forward slash. Anything else — including backslashes and '..' — is rejected.
    private static readonly Regex _safePathPattern = new(
        @"^[a-zA-Z0-9_./\- ]+$",
        RegexOptions.Compiled);

    private readonly ISerializer _yamlWriter;
    private readonly IDeserializer _yamlReader;

    public TemplateAssetManifest()
    {
        _yamlWriter = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        _yamlReader = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Write the manifest YAML to <paramref name="outputRoot"/>/<see cref="ManifestFileName"/>.
    /// References are sorted by (kind, path) for deterministic diffs across runs.
    /// </summary>
    public void Write(string outputRoot, IEnumerable<TemplateReference> references)
    {
        Directory.CreateDirectory(outputRoot);
        var list = references
            .OrderBy(r => r.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var path = Path.Combine(outputRoot, ManifestFileName);
        File.WriteAllText(path, _yamlWriter.Serialize(list), Encoding.UTF8);
    }

    /// <summary>
    /// Read the manifest from <paramref name="inputRoot"/>. Returns <c>null</c> if no
    /// manifest is present (older baselines lack it — validation becomes a no-op).
    /// Returns empty list if the file exists but contains no entries.
    /// </summary>
    public List<TemplateReference>? Read(string inputRoot)
    {
        var path = Path.Combine(inputRoot, ManifestFileName);
        if (!File.Exists(path)) return null;

        var content = File.ReadAllText(path, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(content)) return new List<TemplateReference>();

        var deserialized = _yamlReader.Deserialize<List<TemplateReference>?>(content);
        return deserialized ?? new List<TemplateReference>();
    }

    /// <summary>
    /// Validate each reference against the filesystem rooted at <paramref name="filesRoot"/>.
    /// Missing files are escalated via <paramref name="escalator"/> (once per reference) —
    /// lenient mode = warning + continue; strict mode = captured for end-of-run failure.
    /// Returns the count of unresolved references.
    /// </summary>
    public int Validate(
        string filesRoot,
        List<TemplateReference> references,
        StrictModeEscalator escalator)
    {
        if (references.Count > MaxReferences)
        {
            escalator.Escalate(
                $"Template manifest rejected: {references.Count} references exceeds cap of {MaxReferences}");
            return references.Count;
        }

        var designsDir = Path.Combine(filesRoot, "Templates", "Designs");
        int missingCount = 0;

        foreach (var r in references)
        {
            if (!IsPathSafe(r.Path))
            {
                missingCount++;
                escalator.Escalate(
                    $"Template manifest contains invalid {r.Kind} path '{r.Path}' " +
                    $"(traversal attempt or disallowed characters) — referenced by: {FormatRefList(r.ReferencedBy)}");
                continue;
            }

            bool found = r.Kind switch
            {
                "page-layout" => FindInAnyDesign(designsDir, r.Path),
                "grid-row" => FindInAnyDesign(designsDir,
                    Path.Combine("Grid", "Page", "RowDefinitions", $"{r.Path}.json")),
                "item-type" => File.Exists(
                    Path.Combine(filesRoot, "System", "Items", $"ItemType_{r.Path}.xml")),
                _ => false
            };

            if (!found)
            {
                missingCount++;
                escalator.Escalate(
                    $"Missing {r.Kind} template: '{r.Path}' — referenced by: {FormatRefList(r.ReferencedBy)}");
            }
        }

        return missingCount;
    }

    private static bool FindInAnyDesign(string designsDir, string relativePath)
    {
        if (!Directory.Exists(designsDir)) return false;
        foreach (var designDir in Directory.EnumerateDirectories(designsDir))
        {
            if (File.Exists(Path.Combine(designDir, relativePath)))
                return true;
        }
        return false;
    }

    private static bool IsPathSafe(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (path.Contains("..")) return false;
        if (path.Contains('\\')) return false;
        if (Path.IsPathRooted(path)) return false;
        return _safePathPattern.IsMatch(path);
    }

    private static string FormatRefList(List<string> referencedBy)
    {
        if (referencedBy.Count == 0) return "(no sources recorded)";
        if (referencedBy.Count <= 5) return string.Join(", ", referencedBy);
        return string.Join(", ", referencedBy.Take(5)) + $", ... ({referencedBy.Count} total)";
    }
}
