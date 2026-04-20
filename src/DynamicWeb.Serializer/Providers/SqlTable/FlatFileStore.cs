using System.Security.Cryptography;
using System.Text;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DynamicWeb.Serializer.Providers.SqlTable;

/// <summary>
/// Per-row YAML file I/O in _sql/{TableName}/ layout.
/// Uses a YAML serializer that preserves nulls (emits as ~) for SQL NULL fidelity.
/// </summary>
public class FlatFileStore
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public FlatFileStore()
    {
        // SQL-specific serializer: preserves null values as ~ (NOT OmitNull like content YAML)
        // ForceStringScalarEmitter selects Literal block style for LF-only multiline strings
        // (pretty-printed XML from XmlFormatter uses LF-only, so this emits readable YAML blocks)
        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEventEmitter(next => new ForceStringScalarEmitter(next))
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .Build();
        _deserializer = YamlConfiguration.BuildDeserializer();
    }

    /// <summary>
    /// Write a single row as a YAML file to _sql/{tableName}/{rowIdentity}.yml. When a non-null
    /// <paramref name="writtenFiles"/> list is supplied, the resolved absolute path is appended
    /// (Phase 37-01 Task 2 — fuels per-mode manifest cleanup).
    /// </summary>
    public void WriteRow(string outputRoot, string tableName, string rowIdentity,
        Dictionary<string, object?> rowData, HashSet<string>? usedNames = null,
        List<string>? writtenFiles = null)
    {
        var directory = Path.Combine(outputRoot, "_sql", tableName);
        Directory.CreateDirectory(directory);

        var sanitized = SanitizeFileName(rowIdentity);
        var fileName = DeduplicateFileName(sanitized, rowIdentity, usedNames);
        var filePath = Path.Combine(directory, fileName + ".yml");

        var yaml = _serializer.Serialize(rowData);
        File.WriteAllText(filePath, yaml, Encoding.UTF8);

        writtenFiles?.Add(Path.GetFullPath(filePath));
    }

    /// <summary>
    /// Write table metadata as _meta.yml. Appends the path to <paramref name="writtenFiles"/>
    /// when supplied.
    /// </summary>
    public void WriteMeta(string outputRoot, string tableName, TableMetadata metadata,
        List<string>? writtenFiles = null)
    {
        var directory = Path.Combine(outputRoot, "_sql", tableName);
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, "_meta.yml");
        var yaml = _serializer.Serialize(metadata);
        File.WriteAllText(filePath, yaml, Encoding.UTF8);

        writtenFiles?.Add(Path.GetFullPath(filePath));
    }

    /// <summary>
    /// Read all row YAML files from _sql/{tableName}/, excluding _meta.yml.
    /// </summary>
    public IEnumerable<Dictionary<string, object?>> ReadAllRows(string inputRoot, string tableName)
    {
        var directory = Path.Combine(inputRoot, "_sql", tableName);
        if (!Directory.Exists(directory))
            yield break;

        var files = Directory.EnumerateFiles(directory, "*.yml")
            .Where(f => !Path.GetFileName(f).Equals("_meta.yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f);

        foreach (var file in files)
        {
            var yaml = File.ReadAllText(file, Encoding.UTF8);
            var row = _deserializer.Deserialize<Dictionary<string, object?>>(yaml);
            // Ensure case-insensitive lookup for column name matching with DB schema
            var caseInsensitive = new Dictionary<string, object?>(
                row ?? new Dictionary<string, object?>(),
                StringComparer.OrdinalIgnoreCase);
            yield return caseInsensitive;
        }
    }

    /// <summary>
    /// Read table metadata from _meta.yml.
    /// </summary>
    public TableMetadata ReadMeta(string inputRoot, string tableName)
    {
        var filePath = Path.Combine(inputRoot, "_sql", tableName, "_meta.yml");
        var yaml = File.ReadAllText(filePath, Encoding.UTF8);
        return _deserializer.Deserialize<TableMetadata>(yaml);
    }

    /// <summary>
    /// Sanitize a file name by replacing invalid characters with underscore.
    /// Follows FileSystemStore.SanitizeFolderName pattern.
    /// </summary>
    public static string SanitizeFileName(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return "_unnamed";

        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(trimmed.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private static string DeduplicateFileName(string sanitized, string originalIdentity, HashSet<string>? usedNames)
    {
        if (usedNames == null)
            return sanitized;

        if (usedNames.Add(sanitized))
            return sanitized;

        // Collision: append first 6 chars of MD5 of original identity
        var hash = Convert.ToHexString(
            MD5.HashData(Encoding.UTF8.GetBytes(originalIdentity))).ToLowerInvariant();
        var deduped = $"{sanitized} [{hash[..6]}]";
        usedNames.Add(deduped);
        return deduped;
    }
}
