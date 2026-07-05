namespace Truvio.Commerce.Serializer.Configuration;

/// <summary>
/// Maps the two deployment modes to their merge-semantics aliases, alias-first:
/// <c>deploy</c> ≡ <c>replace</c> (source-wins) and <c>seed</c> ≡ <c>merge</c>
/// (destination-wins, field-level). The engine accepts BOTH names on input and locates
/// mode subfolders / <c>{label}-manifest.json</c> under either label during the transition,
/// so callers may ship <c>replace/replace-manifest.json</c> or the legacy
/// <c>deploy/deploy-manifest.json</c> and both deserialize. The <see cref="DeploymentMode"/>
/// enum, admin-UI labels, and log keys are deliberately left on the legacy names — the alias
/// is a synonym at the boundary, not a rename of internals.
/// </summary>
public static class DeploymentModeAlias
{
    /// <summary>
    /// Resolve a mode string (<c>deploy</c> | <c>replace</c> | <c>seed</c> | <c>merge</c>,
    /// case-insensitive, surrounding whitespace tolerated) to its conflict-strategy enum and the
    /// normalized on-disk label the caller requested. Returns false for any other string.
    /// </summary>
    public static bool TryResolve(string? input, out DeploymentMode mode, out string label)
    {
        mode = DeploymentMode.Deploy;
        label = "deploy";
        if (string.IsNullOrWhiteSpace(input)) return false;

        switch (input.Trim().ToLowerInvariant())
        {
            case "deploy":  mode = DeploymentMode.Deploy; label = "deploy";  return true;
            case "replace": mode = DeploymentMode.Deploy; label = "replace"; return true;
            case "seed":    mode = DeploymentMode.Seed;   label = "seed";    return true;
            case "merge":   mode = DeploymentMode.Seed;   label = "merge";   return true;
            default:        return false;
        }
    }

    /// <summary>
    /// The on-disk label candidates for a mode, in preferred lookup order: legacy name first
    /// (back-compat), alias second. Used to locate an existing subfolder / manifest when the
    /// caller's requested label is not the one written on disk.
    /// </summary>
    public static IReadOnlyList<string> Candidates(DeploymentMode mode) =>
        mode == DeploymentMode.Deploy
            ? new[] { "deploy", "replace" }
            : new[] { "seed", "merge" };
}
