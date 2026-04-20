using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Configuration;

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
    ///
    /// Phase 37-06 (gap closure for SC-3 / CR-01): exists solely so existing unit tests
    /// that exercise JSON parsing (not identifier validation) can continue to call the
    /// 1-arg Load overload without needing a live DW DB.
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
    /// gap closure: this overload used to pass <c>null</c> for the validator, silently
    /// skipping the SQL-identifier allowlist gate. It now constructs a default
    /// <see cref="SqlIdentifierValidator"/> (or uses <see cref="TestOverrideIdentifierValidator"/>
    /// when tests install one) and delegates to the 2-arg overload with a NON-NULL
    /// validator. All 22+ production call sites of <c>ConfigLoader.Load(path)</c> therefore
    /// receive the identifier-validation gate by default, closing Phase-37 SC-3
    /// (malicious table/column identifiers in Serializer.config.json are rejected before
    /// any SQL runs).
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
            // Phase 37-06 Task 1 spy hook — lets structural-integration tests prove the
            // default validator was constructed without having to catch an unrelated
            // DB-layer exception from INFORMATION_SCHEMA when no DW DB is reachable.
            _testDefaultValidatorConstructedCallback.Value?.Invoke();
        }
        return Load(filePath, validator);
    }

    /// <summary>
    /// Load a serializer config. When <paramref name="identifierValidator"/> is non-null,
    /// every SqlTable predicate is checked: Table / NameColumn / ExcludeFields / IncludeFields /
    /// XmlColumns identifiers must exist in INFORMATION_SCHEMA, and any Where clause must
    /// pass <see cref="SqlWhereClauseValidator"/>. Errors across multiple predicates are
    /// aggregated and thrown as a single <see cref="InvalidOperationException"/>.
    /// Tests pass fixture validators; production call sites construct the default
    /// <see cref="SqlIdentifierValidator"/> which queries the live DB.
    ///
    /// Passing <c>null</c> explicitly SKIPS identifier validation — this path is intended
    /// for unit tests that exercise non-validation behavior. Production code should call
    /// the parameterless <see cref="Load(string)"/> overload, which supplies a default
    /// validator automatically.
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

        var (deploy, seed) = BuildModeConfigs(raw);

        var config = new SerializerConfiguration
        {
            OutputDirectory = raw.OutputDirectory!,
            LogLevel = string.IsNullOrWhiteSpace(raw.LogLevel) ? "info" : raw.LogLevel,
            DryRun = raw.DryRun ?? false,
            StrictMode = raw.StrictMode,
            Deploy = deploy,
            Seed = seed
        };

        if (identifierValidator != null)
            ValidateIdentifiers(config, identifierValidator, new SqlWhereClauseValidator());

        // Phase 37-04 / CACHE-01: every ServiceCaches entry on every predicate
        // (Deploy + Seed) must resolve against DwCacheServiceRegistry. Unknown
        // names would otherwise only surface mid-run as CacheInvalidator throws;
        // we want them caught at config-load so the CI/CD pipeline fails fast.
        ValidateServiceCaches(config);

        return config;
    }

    /// <summary>
    /// Phase 37-04: resolve every <c>serviceCaches</c> entry against
    /// <see cref="DwCacheServiceRegistry"/>. Errors accumulate and throw as a
    /// single aggregated <see cref="InvalidOperationException"/> listing the
    /// unknown name, the owning predicate, and the set of supported names.
    /// </summary>
    private static void ValidateServiceCaches(SerializerConfiguration config)
    {
        var errors = new List<string>();

        void Check(ProviderPredicateDefinition p, string scope)
        {
            if (p.ServiceCaches.Count == 0) return;
            foreach (var name in p.ServiceCaches)
            {
                if (DwCacheServiceRegistry.Resolve(name) is null)
                    errors.Add($"{scope} '{p.Name}': cache service '{name}' is not in DwCacheServiceRegistry.");
            }
        }

        foreach (var p in config.Deploy.Predicates) Check(p, "deploy.predicates");
        foreach (var p in config.Seed.Predicates) Check(p, "seed.predicates");

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
                return; // subsequent column checks would be noise if the table itself is bad
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
            // Phase 37-05: ResolveLinksInColumns identifiers must be real columns on the table.
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

        foreach (var p in config.Deploy.Predicates) Check(p, "deploy.predicates");
        foreach (var p in config.Seed.Predicates) Check(p, "seed.predicates");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Configuration is invalid — identifier / WHERE-clause validation failed:\n  - " +
                string.Join("\n  - ", errors));
    }

    private static void Validate(RawSerializerConfiguration raw)
    {
        if (string.IsNullOrWhiteSpace(raw.OutputDirectory))
            throw new InvalidOperationException("Configuration is invalid: 'outputDirectory' is required and must not be empty.");

        // Top-level legacy predicates (optional). Validate each — same rules as before.
        if (raw.Predicates != null)
            ValidatePredicates(raw.Predicates, "predicates");

        // Deploy / Seed sections — each has its own predicates list that needs the same validation.
        if (raw.Deploy?.Predicates != null)
            ValidatePredicates(raw.Deploy.Predicates, "deploy.predicates");
        if (raw.Seed?.Predicates != null)
            ValidatePredicates(raw.Seed.Predicates, "seed.predicates");

        ValidateSubfolder(raw.Deploy?.OutputSubfolder, "deploy.outputSubfolder");
        ValidateSubfolder(raw.Seed?.OutputSubfolder, "seed.outputSubfolder");
    }

    private static void ValidatePredicates(List<RawPredicateDefinition> predicates, string scope)
    {
        for (var i = 0; i < predicates.Count; i++)
        {
            var p = predicates[i];
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new InvalidOperationException($"Configuration is invalid: {scope}[{i}] is missing required field 'name'.");

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

    /// <summary>
    /// Resolve the Deploy + Seed ModeConfigs from the raw JSON. Handles three shapes (D-05):
    ///  1. Both Deploy and legacy top-level Predicates → throw with the explicit message.
    ///  2. Legacy-only flat config → migrate predicates + exclusion dictionaries into Deploy.
    ///  3. New Deploy/Seed shape (or empty) → load each section directly; Seed stays empty if absent.
    /// </summary>
    private static (ModeConfig deploy, ModeConfig seed) BuildModeConfigs(RawSerializerConfiguration raw)
    {
        var hasLegacy = raw.Predicates != null && raw.Predicates.Count > 0;
        var hasDeploy = raw.Deploy != null;
        var hasSeed = raw.Seed != null;

        if (hasLegacy && hasDeploy)
        {
            throw new InvalidOperationException(
                "Configuration is invalid: Both top-level 'Predicates' and 'Deploy.Predicates' are present — " +
                "remove the legacy 'Predicates' field (Phase 37-01 migrated the top-level list to deploy.predicates).");
        }

        ModeConfig deploy;
        if (hasLegacy)
        {
            Console.Error.WriteLine(
                "[Serializer] Migrating legacy flat Predicates → Deploy.Predicates " +
                "(no backcompat; rewriting on next save)");

            deploy = new ModeConfig
            {
                OutputSubfolder = "deploy",
                ConflictStrategy = ConflictStrategy.SourceWins,
                Predicates = raw.Predicates!.Select(BuildPredicate).ToList(),
                ExcludeFieldsByItemType = raw.ExcludeFieldsByItemType ?? new Dictionary<string, List<string>>(),
                ExcludeXmlElementsByType = raw.ExcludeXmlElementsByType ?? new Dictionary<string, List<string>>()
            };
        }
        else if (hasDeploy)
        {
            deploy = BuildModeConfig(raw.Deploy!, defaultSubfolder: "deploy", defaultStrategy: ConflictStrategy.SourceWins);
        }
        else
        {
            // Neither legacy nor Deploy — empty Deploy mode.
            deploy = new ModeConfig
            {
                OutputSubfolder = "deploy",
                ConflictStrategy = ConflictStrategy.SourceWins,
                ExcludeFieldsByItemType = raw.ExcludeFieldsByItemType ?? new Dictionary<string, List<string>>(),
                ExcludeXmlElementsByType = raw.ExcludeXmlElementsByType ?? new Dictionary<string, List<string>>()
            };
        }

        var seed = hasSeed
            ? BuildModeConfig(raw.Seed!, defaultSubfolder: "seed", defaultStrategy: ConflictStrategy.DestinationWins)
            : new ModeConfig
            {
                OutputSubfolder = "seed",
                ConflictStrategy = ConflictStrategy.DestinationWins
            };

        return (deploy, seed);
    }

    private static ModeConfig BuildModeConfig(RawModeSection raw, string defaultSubfolder, ConflictStrategy defaultStrategy)
    {
        return new ModeConfig
        {
            OutputSubfolder = string.IsNullOrEmpty(raw.OutputSubfolder) ? defaultSubfolder : raw.OutputSubfolder!,
            ConflictStrategy = ParseConflictStrategy(raw.ConflictStrategy, defaultStrategy),
            Predicates = raw.Predicates?.Select(BuildPredicate).ToList() ?? new List<ProviderPredicateDefinition>(),
            ExcludeFieldsByItemType = raw.ExcludeFieldsByItemType ?? new Dictionary<string, List<string>>(),
            ExcludeXmlElementsByType = raw.ExcludeXmlElementsByType ?? new Dictionary<string, List<string>>(),
            AcknowledgedOrphanPageIds = raw.AcknowledgedOrphanPageIds ?? new List<int>()
        };
    }

    private static ConflictStrategy ParseConflictStrategy(string? value, ConflictStrategy fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        return value.ToLowerInvariant() switch
        {
            "source-wins" => ConflictStrategy.SourceWins,
            "destination-wins" => ConflictStrategy.DestinationWins,
            _ => fallback
        };
    }

    private static ProviderPredicateDefinition BuildPredicate(RawPredicateDefinition raw) => new()
    {
        Name = raw.Name!,
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

        // Legacy top-level fields (migrated to Deploy.*)
        public string? ConflictStrategy { get; set; }
        public List<RawPredicateDefinition>? Predicates { get; set; }
        public Dictionary<string, List<string>>? ExcludeFieldsByItemType { get; set; }
        public Dictionary<string, List<string>>? ExcludeXmlElementsByType { get; set; }

        // Phase 37-01: Deploy / Seed sections
        public RawModeSection? Deploy { get; set; }
        public RawModeSection? Seed { get; set; }
    }

    private sealed class RawModeSection
    {
        public string? OutputSubfolder { get; set; }
        public string? ConflictStrategy { get; set; }
        public List<RawPredicateDefinition>? Predicates { get; set; }
        public Dictionary<string, List<string>>? ExcludeFieldsByItemType { get; set; }
        public Dictionary<string, List<string>>? ExcludeXmlElementsByType { get; set; }
        public List<int>? AcknowledgedOrphanPageIds { get; set; }
    }

    private sealed class RawPredicateDefinition
    {
        public string? Name { get; set; }
        public string? ProviderType { get; set; }
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

        // Phase 37-03: SqlTable WHERE clause + runtime-exclude opt-in.
        public string? Where { get; set; }
        public List<string>? IncludeFields { get; set; }

        // Phase 37-05 / LINK-02 pass 2: SqlTable column opt-in for at-deserialize
        // Default.aspx?ID=N link rewriting (see ProviderPredicateDefinition).
        public List<string>? ResolveLinksInColumns { get; set; }
        public List<int>? AcknowledgedOrphanPageIds { get; set; }
    }
}
