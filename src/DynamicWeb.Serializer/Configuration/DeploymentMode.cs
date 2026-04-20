namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// Selects between Deploy (source-wins, ships deployment data like shop structure)
/// and Seed (destination-wins, ships one-time seed content without overwriting customer edits).
/// Per Phase 37 CONTEXT.md D-01..D-06: the split lives at config *structure* level — each
/// mode has its own predicate list and exclusion config — not as a per-predicate field.
/// </summary>
public enum DeploymentMode
{
    Deploy,
    Seed
}
