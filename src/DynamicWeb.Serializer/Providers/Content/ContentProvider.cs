using Dynamicweb.Content;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Serialization;

namespace DynamicWeb.Serializer.Providers.Content;

/// <summary>
/// ISerializationProvider adapter for content serialization.
/// Wraps existing ContentSerializer/ContentDeserializer without modifying their internals.
/// Routes content YAML to/from _content/ subdirectory under the output/input root.
/// </summary>
public class ContentProvider : ISerializationProvider
{
    private readonly string? _filesRoot;

    public string ProviderType => "Content";
    public string DisplayName => "Content Provider";

    /// <summary>
    /// Creates a new ContentProvider.
    /// </summary>
    /// <param name="filesRoot">
    /// Optional path to the Files/ root directory, needed by ContentDeserializer for template validation.
    /// </param>
    public ContentProvider(string? filesRoot = null)
    {
        _filesRoot = filesRoot;
    }

    public ValidationResult ValidatePredicate(ProviderPredicateDefinition predicate)
    {
        if (!string.Equals(predicate.ProviderType, "Content", StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Failure("Provider type mismatch: expected 'Content'");

        if (string.IsNullOrWhiteSpace(predicate.Path))
            return ValidationResult.Failure("Path is required for Content predicates");

        if (predicate.AreaId <= 0)
            return ValidationResult.Failure("AreaId must be > 0 for Content predicates");

        return ValidationResult.Success();
    }

    public SerializeResult Serialize(ProviderPredicateDefinition predicate, string outputRoot, Action<string>? log = null)
    {
        var validation = ValidatePredicate(predicate);
        if (!validation.IsValid)
        {
            return new SerializeResult
            {
                TableName = "Content",
                Errors = validation.Errors
            };
        }

        try
        {
            var contentDir = Path.Combine(outputRoot, "_content");
            Directory.CreateDirectory(contentDir);

            var config = BuildSerializerConfiguration(predicate, contentDir);
            var serializer = new ContentSerializer(config, log: log);
            serializer.Serialize();

            // Track written files for the per-mode manifest (Phase 37-01 Task 2).
            // ContentSerializer writes its tree exclusively under contentDir/_content — enumerating
            // *.yml after the run captures everything emitted by the current predicate run. The
            // manifest is per-mode so emissions from parallel predicates into the same mode folder
            // all aggregate up into the OrchestratorResult pool.
            var writtenFiles = Directory.Exists(contentDir)
                ? Directory.GetFiles(contentDir, "*.yml", SearchOption.AllDirectories)
                    .Select(Path.GetFullPath)
                    .ToList()
                : new List<string>();

            return new SerializeResult
            {
                RowsSerialized = writtenFiles.Count,
                TableName = "Content",
                WrittenFiles = writtenFiles
            };
        }
        catch (Exception ex)
        {
            log?.Invoke($"ERROR: Content serialization failed: {ex.Message}");
            return new SerializeResult
            {
                TableName = "Content",
                Errors = new[] { ex.Message }
            };
        }
    }

    public ProviderDeserializeResult Deserialize(
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
                TableName = "Content",
                Errors = validation.Errors
            };
        }

        try
        {
            // Clear area cache — when SqlTable predicates insert Area rows before Content
            // runs, DW's cached AreaService may still return stale data
            try { Services.Areas.ClearCache(); }
            catch { /* ignore if cache clear fails */ }

            var contentDir = Path.Combine(inputRoot, "_content");

            // Fall back to inputRoot if _content/ subdirectory doesn't exist
            // (supports zips created by ad-hoc serialize which don't use the _content/ prefix)
            if (!Directory.Exists(contentDir))
                contentDir = inputRoot;

            var config = BuildSerializerConfiguration(predicate, contentDir);
            var deserializer = new ContentDeserializer(
                config,
                log: log,
                isDryRun: isDryRun,
                filesRoot: _filesRoot,
                conflictStrategy: strategy);
            var result = deserializer.Deserialize();

            if (strategy == ConflictStrategy.DestinationWins)
                log?.Invoke($"Content provider running in DestinationWins (Seed) mode — pages whose PageUniqueId is already present on target are preserved.");

            return new ProviderDeserializeResult
            {
                Created = result.Created,
                Updated = result.Updated,
                Skipped = result.Skipped,
                Failed = result.Failed,
                TableName = "Content",
                Errors = result.Errors.ToList()
            };
        }
        catch (Exception ex)
        {
            log?.Invoke($"ERROR: Content deserialization failed: {ex.Message}");
            return new ProviderDeserializeResult
            {
                TableName = "Content",
                Errors = new[] { ex.Message }
            };
        }
    }

    /// <summary>
    /// Builds a SerializerConfiguration with a single predicate for delegation to ContentSerializer/ContentDeserializer.
    /// </summary>
    private static SerializerConfiguration BuildSerializerConfiguration(ProviderPredicateDefinition predicate, string outputDirectory)
    {
        return new SerializerConfiguration
        {
            OutputDirectory = outputDirectory,
            Predicates = new List<ProviderPredicateDefinition> { predicate }
        };
    }
}
