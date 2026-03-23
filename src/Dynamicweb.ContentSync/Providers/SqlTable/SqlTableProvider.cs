using Dynamicweb.ContentSync.Models;

namespace Dynamicweb.ContentSync.Providers.SqlTable;

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

    public override string ProviderType => "SqlTable";
    public override string DisplayName => "SQL Table Provider";

    public SqlTableProvider(DataGroupMetadataReader metadataReader, SqlTableReader tableReader, FlatFileStore fileStore, SqlTableWriter writer)
    {
        _metadataReader = metadataReader;
        _tableReader = tableReader;
        _fileStore = fileStore;
        _writer = writer;
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

        var metadata = _metadataReader.GetTableMetadata(predicate.DataGroupId!);
        Log($"Serializing table {metadata.TableName} (DataGroup: {predicate.DataGroupId})", log);

        var rows = _tableReader.ReadAllRows(metadata.TableName).ToList();
        Log($"Read {rows.Count} rows from {metadata.TableName}", log);

        _fileStore.WriteMeta(outputRoot, metadata.TableName, metadata);

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var identity = _tableReader.GenerateRowIdentity(row, metadata);
            _fileStore.WriteRow(outputRoot, metadata.TableName, identity, row, usedNames);
        }

        Log($"Serialized {rows.Count} rows to _sql/{metadata.TableName}/", log);

        return new SerializeResult
        {
            RowsSerialized = rows.Count,
            TableName = metadata.TableName
        };
    }

    public override ProviderDeserializeResult Deserialize(ProviderPredicateDefinition predicate, string inputRoot, Action<string>? log = null, bool isDryRun = false)
    {
        var validation = ValidatePredicate(predicate);
        if (!validation.IsValid)
        {
            return new ProviderDeserializeResult
            {
                Errors = validation.Errors
            };
        }

        var metadata = _metadataReader.GetTableMetadata(predicate.DataGroupId!);
        var yamlRows = _fileStore.ReadAllRows(inputRoot, metadata.TableName).ToList();
        Log($"Deserializing {yamlRows.Count} rows into {metadata.TableName} (isDryRun={isDryRun})", log);

        // Build checksum lookup from existing DB rows for skip-on-unchanged detection
        var existingChecksums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existingRow in _tableReader.ReadAllRows(metadata.TableName))
        {
            var identity = _tableReader.GenerateRowIdentity(existingRow, metadata);
            var checksum = _tableReader.CalculateChecksum(existingRow, metadata);
            existingChecksums[identity] = checksum;
        }

        int created = 0, updated = 0, skipped = 0, failed = 0;
        var errors = new List<string>();

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

            var outcome = _writer.WriteRow(yamlRow, metadata, isDryRun);
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

    public override ValidationResult ValidatePredicate(ProviderPredicateDefinition predicate)
    {
        if (!string.Equals(predicate.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Failure("Provider type mismatch");

        if (string.IsNullOrEmpty(predicate.DataGroupId))
            return ValidationResult.Failure("DataGroupId is required for SqlTable predicates");

        return ValidationResult.Success();
    }
}
