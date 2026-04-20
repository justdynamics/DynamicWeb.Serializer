using System.Text.Json;
using System.Text.RegularExpressions;
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

    public static SerializerConfiguration Load(string filePath)
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

        return new SerializerConfiguration
        {
            OutputDirectory = raw.OutputDirectory!,
            LogLevel = string.IsNullOrWhiteSpace(raw.LogLevel) ? "info" : raw.LogLevel,
            DryRun = raw.DryRun ?? false,
            Deploy = deploy,
            Seed = seed
        };
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
            ExcludeXmlElementsByType = raw.ExcludeXmlElementsByType ?? new Dictionary<string, List<string>>()
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
        ExcludeAreaColumns = raw.ExcludeAreaColumns ?? new List<string>()
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
    }
}
