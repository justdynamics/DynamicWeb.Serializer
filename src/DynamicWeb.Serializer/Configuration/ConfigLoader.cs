using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// Reads <see cref="SerializerConfiguration"/> from JSON. Phase 40 (D-01..D-04) flat shape:
/// a single top-level <c>predicates</c> array where every entry carries its own <c>mode</c>
/// (Deploy/Seed). Top-level <c>deploy</c> / <c>seed</c> objects are HARD-REJECTED with a clear
/// actionable error — no silent migration. Top-level <c>predicates</c> entries missing the
/// <c>mode</c> field are likewise rejected. Per-predicate <c>mode</c> values must parse
/// case-insensitively to <see cref="DeploymentMode"/> (i.e. "Deploy" or "Seed").
/// </summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // T-37-01-02: OutputSubfolder path-traversal guard. Alphanumeric + underscore + dash, 1..32 chars.
    private static readonly Regex _safeSubfolder = new("^[a-zA-Z0-9_-]{1,32}$", RegexOptions.Compiled);

    /// <summary>
    /// Test-only override of the default SqlIdentifierValidator used by the 1-arg
    /// <see cref="Load(string)"/> overload. When non-null, <see cref="Load(string)"/>
    /// delegates to the 2-arg overload with THIS validator; when null, it constructs
    /// a fresh <see cref="SqlIdentifierValidator"/> (which queries INFORMATION_SCHEMA
    /// via the live Dynamicweb DB connection).
    ///
    /// Uses <see cref="AsyncLocal{T}"/> so parallel xUnit test workers do not leak
    /// overrides between tests. Mirrors the pattern in
    /// <see cref="ConfigPathResolver.TestOverridePath"/>. NOT intended for production.
    /// </summary>
    private static readonly AsyncLocal<SqlIdentifierValidator?> _testOverrideIdentifierValidator = new();
    public static SqlIdentifierValidator? TestOverrideIdentifierValidator
    {
        get => _testOverrideIdentifierValidator.Value;
        set => _testOverrideIdentifierValidator.Value = value;
    }

    /// <summary>
    /// Test-only spy hook invoked by the 1-arg <see cref="Load(string)"/> overload
    /// when it constructs a DEFAULT <see cref="SqlIdentifierValidator"/> (i.e.
    /// <see cref="TestOverrideIdentifierValidator"/> was null). Exists so a structural
    /// test can prove the default validator was built without relying on catching a
    /// non-specific DB-layer exception.
    /// </summary>
    internal static readonly AsyncLocal<Action?> _testDefaultValidatorConstructedCallback = new();

    /// <summary>
    /// Load a serializer config with default identifier validation enabled. Phase 37-06
    /// gap closure: this overload constructs a default <see cref="SqlIdentifierValidator"/>
    /// (or uses <see cref="TestOverrideIdentifierValidator"/> when tests install one) and
    /// delegates to the 2-arg overload with a NON-NULL validator. All production call sites
    /// of <c>ConfigLoader.Load(path)</c> therefore receive the identifier-validation gate
    /// by default, closing Phase-37 SC-3.
    /// </summary>
    public static SerializerConfiguration Load(string filePath)
    {
        var overrideValidator = TestOverrideIdentifierValidator;
        SqlIdentifierValidator validator;
        if (overrideValidator != null)
        {
            validator = overrideValidator;
        }
        else
        {
            validator = new SqlIdentifierValidator();
            _testDefaultValidatorConstructedCallback.Value?.Invoke();
        }
        return Load(filePath, validator);
    }

    /// <summary>
    /// Load a serializer config. When <paramref name="identifierValidator"/> is non-null,
    /// every SqlTable predicate is checked: Table / NameColumn / ExcludeFields / IncludeFields /
    /// XmlColumns / ResolveLinksInColumns identifiers must exist in INFORMATION_SCHEMA, and any
    /// Where clause must pass <see cref="SqlWhereClauseValidator"/>. Errors across multiple
    /// predicates are aggregated and thrown as a single <see cref="InvalidOperationException"/>.
    ///
    /// Passing <c>null</c> explicitly SKIPS identifier validation — this path is intended
    /// for unit tests that exercise non-validation behavior. Production code should call
    /// the parameterless <see cref="Load(string)"/> overload.
    /// </summary>
    public static SerializerConfiguration Load(string filePath, SqlIdentifierValidator? identifierValidator)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Configuration file not found: '{filePath}'", filePath);

        var json = File.ReadAllText(filePath);

        var raw = JsonSerializer.Deserialize<RawSerializerConfiguration>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize configuration file — result was null.");

        Validate(raw);

        if (!Directory.Exists(raw.OutputDirectory))
        {
            Console.Error.WriteLine(
                $"[Serializer] Warning: OutputDirectory '{raw.OutputDirectory}' does not exist. " +
                "Serialization will create it; deserialization requires it to exist.");
        }

        var predicates = raw.Predicates?.Select(BuildPredicate).ToList() ?? new List<ProviderPredicateDefinition>();

        var config = new SerializerConfiguration
        {
            OutputDirectory = raw.OutputDirectory!,
            LogLevel = string.IsNullOrWhiteSpace(raw.LogLevel) ? "info" : raw.LogLevel,
            DryRun = raw.DryRun ?? false,
            StrictMode = raw.StrictMode,
            DeployOutputSubfolder = string.IsNullOrEmpty(raw.DeployOutputSubfolder) ? "deploy" : raw.DeployOutputSubfolder!,
            SeedOutputSubfolder = string.IsNullOrEmpty(raw.SeedOutputSubfolder) ? "seed" : raw.SeedOutputSubfolder!,
            ExcludeFieldsByItemType = raw.ExcludeFieldsByItemType ?? new Dictionary<string, List<string>>(),
            ExcludeXmlElementsByType = raw.ExcludeXmlElementsByType ?? new Dictionary<string, List<string>>(),
            Predicates = predicates
        };

        if (identifierValidator != null)
            ValidateIdentifiers(config, identifierValidator, new SqlWhereClauseValidator());

        // Phase 37-04 / CACHE-01: every ServiceCaches entry must resolve against
        // DwCacheServiceRegistry. Unknown names would otherwise only surface mid-run.
        ValidateServiceCaches(config);

        return config;
    }

    /// <summary>
    /// Phase 37-04: resolve every <c>serviceCaches</c> entry against
    /// <see cref="DwCacheServiceRegistry"/>. Errors accumulate and throw as a
    /// single aggregated <see cref="InvalidOperationException"/>.
    /// </summary>
    private static void ValidateServiceCaches(SerializerConfiguration config)
    {
        var errors = new List<string>();

        foreach (var p in config.Predicates)
        {
            if (p.ServiceCaches.Count == 0) continue;
            foreach (var name in p.ServiceCaches)
            {
                if (DwCacheServiceRegistry.Resolve(name) is null)
                    errors.Add($"predicates '{p.Name}': cache service '{name}' is not in DwCacheServiceRegistry.");
            }
        }

        if (errors.Count == 0) return;

        var supported = DwCacheServiceRegistry.AllSupportedNames;
        var previewCount = Math.Min(20, supported.Count);
        var preview = string.Join(", ", supported.Take(previewCount));
        var suffix = supported.Count > previewCount
            ? $" (+{supported.Count - previewCount} more)"
            : "";

        throw new InvalidOperationException(
            "Configuration is invalid — ServiceCaches validation failed:\n  - " +
            string.Join("\n  - ", errors) +
            $"\nSupported ({supported.Count} total): {preview}{suffix}.\n" +
            "See DwCacheServiceRegistry.cs — add new entries by PR.");
    }

    /// <summary>
    /// Phase 37-03: validate every SqlTable predicate identifier (table, columns in Exclude/
    /// Include/Xml/NameColumn, and Where-clause references) against INFORMATION_SCHEMA via
    /// the provided validator. Errors accumulate; a single aggregated exception is thrown at
    /// the end if any predicate failed.
    /// </summary>
    private static void ValidateIdentifiers(
        SerializerConfiguration config,
        SqlIdentifierValidator idValidator,
        SqlWhereClauseValidator whereValidator)
    {
        var errors = new List<string>();

        void Check(ProviderPredicateDefinition p, string scope)
        {
            if (!string.Equals(p.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase)) return;
            if (string.IsNullOrWhiteSpace(p.Table))
            {
                errors.Add($"{scope}: SqlTable predicate '{p.Name}' is missing 'table'.");
                return;
            }

            // 1. Table identifier.
            try { idValidator.ValidateTable(p.Table!); }
            catch (InvalidOperationException ex)
            {
                errors.Add($"{scope} '{p.Name}': {ex.Message}");
                return;
            }

            // 2. Column-level identifiers — NameColumn, each ExcludeFields/IncludeFields/XmlColumns entry.
            if (!string.IsNullOrWhiteSpace(p.NameColumn))
            {
                try { idValidator.ValidateColumn(p.Table!, p.NameColumn!); }
                catch (InvalidOperationException ex) { errors.Add($"{scope} '{p.Name}': {ex.Message}"); }
            }
            foreach (var col in p.ExcludeFields)
            {
                try { idValidator.ValidateColumn(p.Table!, col); }
                catch (InvalidOperationException ex) { errors.Add($"{scope} '{p.Name}': {ex.Message}"); }
            }
            foreach (var col in p.IncludeFields)
            {
                try { idValidator.ValidateColumn(p.Table!, col); }
                catch (InvalidOperationException ex) { errors.Add($"{scope} '{p.Name}': {ex.Message}"); }
            }
            foreach (var col in p.XmlColumns)
            {
                try { idValidator.ValidateColumn(p.Table!, col); }
                catch (InvalidOperationException ex) { errors.Add($"{scope} '{p.Name}': {ex.Message}"); }
            }
            foreach (var col in p.ResolveLinksInColumns)
            {
                try { idValidator.ValidateColumn(p.Table!, col); }
                catch (InvalidOperationException ex) { errors.Add($"{scope} '{p.Name}': {ex.Message}"); }
            }

            // 3. WHERE clause — must parse + every identifier must be an existing column.
            if (!string.IsNullOrWhiteSpace(p.Where))
            {
                try
                {
                    var cols = idValidator.GetColumns(p.Table!);
                    whereValidator.Validate(p.Where!, cols);
                }
                catch (InvalidOperationException ex)
                {
                    errors.Add($"{scope} '{p.Name}': {ex.Message}");
                }
            }
        }

        foreach (var p in config.Predicates) Check(p, "predicates");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Configuration is invalid — identifier / WHERE-clause validation failed:\n  - " +
                string.Join("\n  - ", errors));
    }

    private static void Validate(RawSerializerConfiguration raw)
    {
        if (string.IsNullOrWhiteSpace(raw.OutputDirectory))
            throw new InvalidOperationException("Configuration is invalid: 'outputDirectory' is required and must not be empty.");

        // Phase 40 D-03: legacy section-level shape (top-level 'deploy' or 'seed' object) is
        // hard-rejected. No silent migration. Users must rewrite their config to the flat shape
        // with per-predicate 'mode' fields. See docs/baselines/Swift2.2-baseline.md.
        if (raw.Deploy != null)
            throw new InvalidOperationException(
                "Configuration is invalid: Legacy section-level shape detected — top-level 'deploy' object is no longer supported (Phase 40, per-predicate mode). " +
                "Move every predicate from 'deploy.predicates' into the top-level 'predicates' array and add \"mode\": \"Deploy\" to each. " +
                "See docs/baselines/Swift2.2-baseline.md for the new shape.");
        if (raw.Seed != null)
            throw new InvalidOperationException(
                "Configuration is invalid: Legacy section-level shape detected — top-level 'seed' object is no longer supported (Phase 40, per-predicate mode). " +
                "Move every predicate from 'seed.predicates' into the top-level 'predicates' array and add \"mode\": \"Seed\" to each. " +
                "See docs/baselines/Swift2.2-baseline.md for the new shape.");

        if (raw.Predicates != null)
            ValidatePredicates(raw.Predicates, "predicates");

        ValidateSubfolder(raw.DeployOutputSubfolder, "deployOutputSubfolder");
        ValidateSubfolder(raw.SeedOutputSubfolder, "seedOutputSubfolder");
    }

    private static void ValidatePredicates(List<RawPredicateDefinition> predicates, string scope)
    {
        for (var i = 0; i < predicates.Count; i++)
        {
            var p = predicates[i];
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new InvalidOperationException($"Configuration is invalid: {scope}[{i}] is missing required field 'name'.");

            // Phase 40 D-01: every predicate must declare its mode.
            if (string.IsNullOrWhiteSpace(p.Mode))
                throw new InvalidOperationException(
                    $"Configuration is invalid: {scope}[{i}] (name='{p.Name}') is missing required field 'mode' " +
                    "(expected 'Deploy' or 'Seed', case-insensitive).");
            if (!Enum.TryParse<DeploymentMode>(p.Mode, ignoreCase: true, out _))
                throw new InvalidOperationException(
                    $"Configuration is invalid: {scope}[{i}] (name='{p.Name}') has invalid mode '{p.Mode}' " +
                    "(expected 'Deploy' or 'Seed', case-insensitive).");

            var isContentPredicate = string.IsNullOrEmpty(p.ProviderType)
                || string.Equals(p.ProviderType, "Content", StringComparison.OrdinalIgnoreCase);
            if (isContentPredicate)
            {
                if (string.IsNullOrWhiteSpace(p.Path))
                    throw new InvalidOperationException($"Configuration is invalid: {scope}[{i}] is missing required field 'path'.");
                if (p.AreaId <= 0)
                    throw new InvalidOperationException($"Configuration is invalid: {scope}[{i}] is missing required field 'areaId' (must be > 0).");
            }
        }
    }

    private static void ValidateSubfolder(string? candidate, string scope)
    {
        if (string.IsNullOrEmpty(candidate)) return;
        if (!_safeSubfolder.IsMatch(candidate))
        {
            throw new InvalidOperationException(
                $"Configuration is invalid: {scope} '{candidate}' must match [a-zA-Z0-9_-]{{1,32}} " +
                "(no path separators, no '..', no absolute paths).");
        }
    }

    private static ProviderPredicateDefinition BuildPredicate(RawPredicateDefinition raw)
    {
        // Mode parse is safe — ValidatePredicates already verified the value parses.
        Enum.TryParse<DeploymentMode>(raw.Mode, ignoreCase: true, out var mode);
        return new ProviderPredicateDefinition
        {
            Name = raw.Name!,
            Mode = mode,
            ProviderType = string.IsNullOrEmpty(raw.ProviderType) ? "Content" : raw.ProviderType,
            Path = raw.Path ?? "",
            AreaId = raw.AreaId,
            PageId = raw.PageId,
            Excludes = raw.Excludes ?? new List<string>(),
            Table = raw.Table,
            NameColumn = raw.NameColumn,
            CompareColumns = raw.CompareColumns,
            ServiceCaches = raw.ServiceCaches ?? new List<string>(),
            SchemaSync = raw.SchemaSync,
            XmlColumns = raw.XmlColumns ?? new List<string>(),
            ExcludeFields = raw.ExcludeFields ?? new List<string>(),
            ExcludeXmlElements = raw.ExcludeXmlElements ?? new List<string>(),
            ExcludeAreaColumns = raw.ExcludeAreaColumns ?? new List<string>(),
            Where = string.IsNullOrWhiteSpace(raw.Where) ? null : raw.Where,
            IncludeFields = raw.IncludeFields ?? new List<string>(),
            ResolveLinksInColumns = raw.ResolveLinksInColumns ?? new List<string>(),
            AcknowledgedOrphanPageIds = raw.AcknowledgedOrphanPageIds ?? new List<int>()
        };
    }

    // -------------------------------------------------------------------------
    // Raw DTOs for deserialization — nullable everywhere so we can produce clear
    // validation errors rather than generic JSON ones.
    // -------------------------------------------------------------------------

    private sealed class RawSerializerConfiguration
    {
        public string? OutputDirectory { get; set; }
        public string? LogLevel { get; set; }
        public bool? DryRun { get; set; }

        /// <summary>Phase 37-04 STRICT-01: nullable → entry-point default when omitted.</summary>
        public bool? StrictMode { get; set; }

        // Phase 40 D-02: top-level subfolder names + flat exclusion dictionaries.
        public string? DeployOutputSubfolder { get; set; }
        public string? SeedOutputSubfolder { get; set; }
        public Dictionary<string, List<string>>? ExcludeFieldsByItemType { get; set; }
        public Dictionary<string, List<string>>? ExcludeXmlElementsByType { get; set; }

        // Phase 40 D-02: SINGLE flat predicate list with per-entry Mode.
        public List<RawPredicateDefinition>? Predicates { get; set; }

        // Phase 40 D-03: detection-only fields. If either is non-null after deserialize the
        // legacy section shape was used and Validate() throws. They are NEVER read for content.
        // Using `object?` makes them match any JSON shape (object, array, primitive) without
        // us caring about contents — Validate() throws on any non-null value.
        public object? Deploy { get; set; }
        public object? Seed { get; set; }
    }

    private sealed class RawPredicateDefinition
    {
        public string? Name { get; set; }
        public string? ProviderType { get; set; }

        /// <summary>Phase 40 D-01: required per-predicate mode ("Deploy" or "Seed", case-insensitive).</summary>
        public string? Mode { get; set; }

        public string? Path { get; set; }
        public int AreaId { get; set; }
        public int PageId { get; set; }
        public List<string>? Excludes { get; set; }
        public string? Table { get; set; }
        public string? NameColumn { get; set; }
        public string? CompareColumns { get; set; }
        public List<string>? ServiceCaches { get; set; }
        public string? SchemaSync { get; set; }
        public List<string>? XmlColumns { get; set; }
        public List<string>? ExcludeFields { get; set; }
        public List<string>? ExcludeXmlElements { get; set; }
        public List<string>? ExcludeAreaColumns { get; set; }
        public string? Where { get; set; }
        public List<string>? IncludeFields { get; set; }
        public List<string>? ResolveLinksInColumns { get; set; }
        public List<int>? AcknowledgedOrphanPageIds { get; set; }
    }
}
