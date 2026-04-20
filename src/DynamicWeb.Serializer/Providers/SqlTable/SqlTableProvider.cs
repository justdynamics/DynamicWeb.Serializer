using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Providers.SqlTable;

/// <summary>
/// ISerializationProvider implementation for SQL tables.
/// Reads DataGroup XML metadata, reads SQL table rows, resolves row identity,
/// and writes per-row YAML files to _sql/{TableName}/.
/// Supports full round-trip: Serialize (DB to YAML) and Deserialize (YAML to DB via MERGE).
/// </summary>
public class SqlTableProvider : SerializationProviderBase
{
    private readonly DataGroupMetadataReader _metadataReader;
    private readonly SqlTableReader _tableReader;
    private readonly FlatFileStore _fileStore;
    private readonly SqlTableWriter _writer;
    private readonly TargetSchemaCache _schemaCache;

    public override string ProviderType => "SqlTable";
    public override string DisplayName => "SQL Table Provider";

    /// <summary>
    /// Creates the provider. <paramref name="schemaCache"/> is the Phase 37-02 unified target
    /// schema / type coercion cache; defaults to a fresh instance backed by the live
    /// INFORMATION_SCHEMA loader. Pass a shared instance to coalesce schema queries across
    /// providers within the same deserialize run.
    /// </summary>
    public SqlTableProvider(
        DataGroupMetadataReader metadataReader,
        SqlTableReader tableReader,
        FlatFileStore fileStore,
        SqlTableWriter writer,
        TargetSchemaCache? schemaCache = null)
    {
        _metadataReader = metadataReader;
        _tableReader = tableReader;
        _fileStore = fileStore;
        _writer = writer;
        _schemaCache = schemaCache ?? new TargetSchemaCache();
    }

    public override SerializeResult Serialize(ProviderPredicateDefinition predicate, string outputRoot, Action<string>? log = null)
    {
        var validation = ValidatePredicate(predicate);
        if (!validation.IsValid)
        {
            return new SerializeResult
            {
                Errors = validation.Errors
            };
        }

        var metadata = _metadataReader.GetTableMetadata(predicate, includeColumnDefinitions: true);
        Log($"Serializing table {metadata.TableName}", log);

        var rows = _tableReader.ReadAllRows(metadata.TableName).ToList();
        Log($"Read {rows.Count} rows from {metadata.TableName}", log);

        var writtenFiles = new List<string>();
        _fileStore.WriteMeta(outputRoot, metadata.TableName, metadata, writtenFiles);

        var xmlColumns = new HashSet<string>(predicate.XmlColumns, StringComparer.OrdinalIgnoreCase);
        var excludeFields = predicate.ExcludeFields.Count > 0
            ? new HashSet<string>(predicate.ExcludeFields, StringComparer.OrdinalIgnoreCase)
            : null;

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            // Step 1: Pretty-print XML columns
            if (xmlColumns.Count > 0)
            {
                foreach (var col in xmlColumns)
                {
                    if (row.TryGetValue(col, out var val) && val is string strVal)
                    {
                        row[col] = XmlFormatter.PrettyPrint(strVal);
                    }
                }
            }

            // Step 2: Strip excluded XML elements from XML columns
            if (predicate.ExcludeXmlElements.Count > 0 && xmlColumns.Count > 0)
            {
                foreach (var col in xmlColumns)
                {
                    if (row.TryGetValue(col, out var val) && val is string strVal)
                    {
                        row[col] = XmlFormatter.RemoveElements(strVal, predicate.ExcludeXmlElements);
                    }
                }
            }

            // Step 3: Remove excluded columns from row
            if (excludeFields != null)
            {
                foreach (var field in excludeFields)
                    row.Remove(field);
            }

            var identity = _tableReader.GenerateRowIdentity(row, metadata);
            _fileStore.WriteRow(outputRoot, metadata.TableName, identity, row, usedNames, writtenFiles);
        }

        Log($"Serialized {rows.Count} rows to _sql/{metadata.TableName}/", log);

        return new SerializeResult
        {
            RowsSerialized = rows.Count,
            TableName = metadata.TableName,
            WrittenFiles = writtenFiles
        };
    }

    public override ProviderDeserializeResult Deserialize(
        ProviderPredicateDefinition predicate,
        string inputRoot,
        Action<string>? log = null,
        bool isDryRun = false,
        ConflictStrategy strategy = ConflictStrategy.SourceWins)
    {
        var validation = ValidatePredicate(predicate);
        if (!validation.IsValid)
        {
            return new ProviderDeserializeResult
            {
                Errors = validation.Errors
            };
        }

        var tableName = predicate.Table!;

        // If table doesn't exist in target, create it from serialized metadata
        if (!_metadataReader.TableExists(tableName))
        {
            Log($"Table [{tableName}] does not exist in target — creating from serialized schema", log);

            if (!isDryRun)
            {
                try
                {
                    var serializedMeta = _fileStore.ReadMeta(inputRoot, tableName);
                    _writer.CreateTableFromMetadata(serializedMeta);
                    Log($"Created table [{tableName}]", log);
                }
                catch (Exception ex)
                {
                    Log($"ERROR: Failed to create table [{tableName}]: {ex.Message}", log);
                    return new ProviderDeserializeResult
                    {
                        TableName = tableName,
                        Errors = [$"Failed to create table [{tableName}]: {ex.Message}"]
                    };
                }
            }
        }

        var metadata = _metadataReader.GetTableMetadata(predicate);
        var yamlRows = _fileStore.ReadAllRows(inputRoot, metadata.TableName).ToList();
        Log($"Deserializing {yamlRows.Count} rows into {metadata.TableName} (isDryRun={isDryRun})", log);

        // Phase 37-02: unified schema-drift + type coercion via TargetSchemaCache.
        // Target columns absent from the live target schema are stripped from each row
        // before composing MERGE SQL (prevents "Invalid column name" on cross-env deploys);
        // remaining string values are coerced to proper .NET types for SQL parameterization.
        var targetCols = _schemaCache.GetColumns(metadata.TableName);
        var columnTypes = _schemaCache.GetColumnTypes(metadata.TableName);
        var notNullColumns = _metadataReader.GetNotNullColumns(metadata.TableName);
        // FixNotNullDefaults takes a mutable Dictionary<string,string> — materialize once.
        var columnTypesDict = columnTypes.Count > 0
            ? new Dictionary<string, string>(columnTypes, StringComparer.OrdinalIgnoreCase)
            : _metadataReader.GetColumnTypes(metadata.TableName);
        foreach (var row in yamlRows)
        {
            // Filter target-missing columns (warn once per missing column across all rows).
            if (targetCols.Count > 0)
            {
                var keysToRemove = row.Keys.Where(k => !targetCols.Contains(k)).ToList();
                foreach (var k in keysToRemove)
                {
                    _schemaCache.LogMissingColumnOnce(metadata.TableName, k, log);
                    row.Remove(k);
                }
            }

            // Coerce remaining column values via the shared cache.
            foreach (var col in row.Keys.ToList())
            {
                var coerced = _schemaCache.Coerce(metadata.TableName, col, row[col]);
                // Coerce returns DBNull.Value for null/DBNull/empty-non-string cases; the downstream
                // row shape uses null (not DBNull) to represent "no value", so re-normalize here —
                // preserves the pre-refactor semantic contract of the row dictionary.
                row[col] = coerced == DBNull.Value ? null : coerced;
            }

            FixNotNullDefaults(row, columnTypesDict, notNullColumns);
            if (predicate.XmlColumns.Count > 0)
                CompactXmlColumns(row, predicate.XmlColumns);
        }

        // Disable FK constraints during deserialization to avoid ordering issues
        if (!isDryRun)
        {
            try { _writer.DisableForeignKeys(metadata.TableName); }
            catch { /* Table may not have FK constraints */ }
        }

        int created = 0, updated = 0, skipped = 0, failed = 0;
        var errors = new List<string>();

        // Tables without primary keys: use truncate+insert strategy
        if (metadata.KeyColumns.Count == 0)
        {
            Log($"  Table [{metadata.TableName}] has no primary key — using truncate+insert strategy", log);
            if (!isDryRun)
            {
                try
                {
                    _writer.TruncateAndInsertAll(yamlRows, metadata, log);
                    created = yamlRows.Count;
                }
                catch (Exception ex)
                {
                    Log($"  ERROR: truncate+insert failed for [{metadata.TableName}]: {ex.Message}", log);
                    failed = yamlRows.Count;
                    errors.Add($"Truncate+insert failed: {ex.Message}");
                }
            }
            else
            {
                created = yamlRows.Count;
            }
        }
        else
        {
            // Build checksum lookup from existing DB rows for skip-on-unchanged detection
            var existingChecksums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var existingRow in _tableReader.ReadAllRows(metadata.TableName))
            {
                var identity = _tableReader.GenerateRowIdentity(existingRow, metadata);
                var checksum = _tableReader.CalculateChecksum(existingRow, metadata);
                existingChecksums[identity] = checksum;
            }

            foreach (var yamlRow in yamlRows)
            {
                var identity = _tableReader.GenerateRowIdentity(yamlRow, metadata);
                var incomingChecksum = _tableReader.CalculateChecksum(yamlRow, metadata);

                // Skip if existing row has identical checksum (no actual change)
                if (existingChecksums.TryGetValue(identity, out var existingChecksum)
                    && string.Equals(incomingChecksum, existingChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    Log($"  Skipped {identity} (unchanged)", log);
                    continue;
                }

                // Seed mode (D-06, Phase 37-01): rows already present on target stay untouched.
                // We rely on the existingChecksums lookup above — if the identity is already keyed,
                // the target has this row, so we skip without issuing the MERGE.
                if (strategy == ConflictStrategy.DestinationWins
                    && existingChecksums.ContainsKey(identity))
                {
                    skipped++;
                    Log($"  Seed-skip: [{metadata.TableName}].{identity} (already present)", log);
                    continue;
                }

                var outcome = _writer.WriteRow(yamlRow, metadata, isDryRun, log, notNullColumns);
                switch (outcome)
                {
                    case WriteOutcome.Created:
                        created++;
                        break;
                    case WriteOutcome.Updated:
                        updated++;
                        break;
                    case WriteOutcome.Failed:
                        failed++;
                        errors.Add($"Failed to write row: {identity}");
                        break;
                }

                Log($"  {outcome} {identity}", log);
            }
        }

        // Re-enable FK constraints
        if (!isDryRun)
        {
            try { _writer.EnableForeignKeys(metadata.TableName); }
            catch (Exception ex) { Log($"  WARNING: Could not re-enable FK constraints for [{metadata.TableName}]: {ex.Message}", log); }
        }

        Log($"Deserialization complete: {created} created, {updated} updated, {skipped} skipped, {failed} failed", log);

        return new ProviderDeserializeResult
        {
            Created = created,
            Updated = updated,
            Skipped = skipped,
            Failed = failed,
            TableName = metadata.TableName,
            Errors = errors
        };
    }

    /// <summary>
    /// Replace null values with type-appropriate defaults for NOT NULL columns.
    /// Prevents "cannot insert NULL" errors during MERGE upsert.
    /// </summary>
    private static void FixNotNullDefaults(Dictionary<string, object?> row, Dictionary<string, string> columnTypes, HashSet<string> notNullColumns)
    {
        foreach (var col in notNullColumns)
        {
            if (!row.ContainsKey(col)) continue;
            if (row[col] is not null) continue;

            // Substitute appropriate default for NOT NULL columns with null YAML values
            if (columnTypes.TryGetValue(col, out var sqlType))
            {
                row[col] = sqlType.ToLowerInvariant() switch
                {
                    "nvarchar" or "varchar" or "nchar" or "char" or "ntext" or "text" or "xml" => "",
                    "int" or "bigint" or "smallint" or "tinyint" => 0,
                    "bit" => false,
                    "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real" => 0m,
                    _ => row[col] // leave as null for types we can't default (let SQL fail with a clear error)
                };
            }
        }
    }

    /// <summary>
    /// Compact XML columns back to single-line before DB write.
    /// Restores compact format so serialize->deserialize->serialize is idempotent.
    /// </summary>
    private static void CompactXmlColumns(Dictionary<string, object?> row, IReadOnlyCollection<string> xmlColumns)
    {
        foreach (var col in xmlColumns)
        {
            if (row.TryGetValue(col, out var val) && val is string strVal)
            {
                row[col] = XmlFormatter.Compact(strVal);
            }
        }
    }

    public override ValidationResult ValidatePredicate(ProviderPredicateDefinition predicate)
    {
        if (!string.Equals(predicate.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Failure("Provider type mismatch");

        if (string.IsNullOrEmpty(predicate.Table))
            return ValidationResult.Failure("Table is required for SqlTable predicates");

        return ValidationResult.Success();
    }
}
