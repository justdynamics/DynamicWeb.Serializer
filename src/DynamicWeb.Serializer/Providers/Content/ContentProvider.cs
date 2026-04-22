using Dynamicweb.Content;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
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

    public SerializeResult Serialize(
        ProviderPredicateDefinition predicate,
        string outputRoot,
        Action<string>? log = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null)
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

            var config = BuildSerializerConfiguration(predicate, contentDir,
                excludeFieldsByItemType, excludeXmlElementsByType);
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
        ConflictStrategy strategy = ConflictStrategy.SourceWins,
        InternalLinkResolver? linkResolver = null,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null)
    {
        // ContentProvider ignores the injected linkResolver — its own deserialize path already
        // builds and applies an InternalLinkResolver for item-field / PropertyItem rewriting.
        // We still accept the parameter to satisfy the ISerializationProvider contract.
        _ = linkResolver;

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

            var config = BuildSerializerConfiguration(predicate, contentDir,
                excludeFieldsByItemType, excludeXmlElementsByType);
            var deserializer = new ContentDeserializer(
                config,
                log: log,
                isDryRun: isDryRun,
                filesRoot: _filesRoot,
                conflictStrategy: strategy);
            var result = deserializer.Deserialize();

            if (strategy == ConflictStrategy.DestinationWins)
                log?.Invoke($"Content provider running in DestinationWins (Seed) mode — pages whose PageUniqueId is already present on target are preserved.");

            // Phase 37-05 / LINK-02 pass 2: after a successful deserialize, build the
            // source→target page ID map from the YAML tree (SourcePageId) + the target DB
            // (by GUID match) so SqlTable predicates in the same orchestrator run can
            // rewrite Default.aspx?ID=N references in configured columns. Skipped on dry-run
            // or when the predicate didn't run (failed area resolution, etc.).
            IReadOnlyDictionary<int, int>? map = null;
            if (!isDryRun)
            {
                try { map = BuildSourceToTargetMap(contentDir); }
                catch (Exception mapEx)
                {
                    log?.Invoke($"WARNING: Could not build source→target page map after Content deserialize: {mapEx.Message}");
                }
            }

            return new ProviderDeserializeResult
            {
                Created = result.Created,
                Updated = result.Updated,
                Skipped = result.Skipped,
                Failed = result.Failed,
                TableName = "Content",
                Errors = result.Errors.ToList(),
                SourceToTargetPageMap = map
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
    /// Phase 37-05 / LINK-02 pass 2: construct the cross-environment page ID map by
    /// reading every area's YAML tree under <paramref name="contentDir"/>, pairing each
    /// page's <c>SourcePageId</c> with the target page resolved by <c>PageUniqueId</c>
    /// (GUID lookup against the live DB). Returns an empty map if no YAML areas are
    /// present or no pages matched.
    /// </summary>
    private static IReadOnlyDictionary<int, int> BuildSourceToTargetMap(string contentDir)
    {
        var allYamlPages = new List<SerializedPage>();
        if (Directory.Exists(contentDir))
        {
            var store = new FileSystemStore();
            foreach (var areaDir in Directory.GetDirectories(contentDir))
            {
                var areaYml = Path.Combine(areaDir, "area.yml");
                if (!File.Exists(areaYml)) continue;
                try
                {
                    var areaData = store.ReadTree(contentDir, Path.GetFileName(areaDir));
                    allYamlPages.AddRange(areaData.Pages);
                }
                catch { /* best-effort — individual unreadable areas skipped */ }
            }
        }

        var allGuidCache = new Dictionary<Guid, int>();
        foreach (var masterArea in Services.Areas.GetAreas())
        {
            foreach (var page in Services.Pages.GetPagesByAreaID(masterArea.ID))
                if (page.UniqueId != Guid.Empty)
                    allGuidCache.TryAdd(page.UniqueId, page.ID);
        }

        return InternalLinkResolver.BuildSourceToTargetMap(allYamlPages, allGuidCache);
    }

    /// <summary>
    /// Builds a SerializerConfiguration with a single predicate for delegation to
    /// ContentSerializer/ContentDeserializer. Threads the parent mode's ItemType + XML-type
    /// exclusion dicts down into the inner ModeConfig — without this, the inner serializer
    /// sees empty dicts and ignores all by-type exclusions.
    /// </summary>
    private static SerializerConfiguration BuildSerializerConfiguration(
        ProviderPredicateDefinition predicate,
        string outputDirectory,
        IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null,
        IReadOnlyDictionary<string, List<string>>? excludeXmlElementsByType = null)
    {
        // Phase 38 A.3 (D-38-03): AcknowledgedOrphanPageIds lives on ProviderPredicateDefinition only.
        // The inner predicate carries its own ack list; ContentSerializer aggregates across predicates.
        return new SerializerConfiguration
        {
            OutputDirectory = outputDirectory,
            Deploy = new ModeConfig
            {
                Predicates = new List<ProviderPredicateDefinition> { predicate },
                ExcludeFieldsByItemType = excludeFieldsByItemType != null
                    ? new Dictionary<string, List<string>>(
                        excludeFieldsByItemType.ToDictionary(kv => kv.Key, kv => kv.Value),
                        StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, List<string>>(),
                ExcludeXmlElementsByType = excludeXmlElementsByType != null
                    ? new Dictionary<string, List<string>>(
                        excludeXmlElementsByType.ToDictionary(kv => kv.Key, kv => kv.Value),
                        StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, List<string>>()
            }
        };
    }
}
