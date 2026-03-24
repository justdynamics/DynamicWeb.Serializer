using DynamicWeb.Serializer.Providers;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Generates actionable advice from error patterns in deserialization results.
/// Maps common error strings to user-friendly guidance.
/// </summary>
public static class AdviceGenerator
{
    public static List<string> GenerateAdvice(OrchestratorResult result)
    {
        var advice = new List<string>();
        bool hasAnyFailed = false;

        foreach (var dr in result.DeserializeResults)
        {
            if (dr.Failed > 0)
                hasAnyFailed = true;

            foreach (var error in dr.Errors)
            {
                if (error.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
                {
                    advice.Add($"FK constraint failed on {dr.TableName} -- check that parent tables are deserialized first (verify predicate ordering)");
                }
                else if (error.Contains("group", StringComparison.OrdinalIgnoreCase) &&
                         error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    advice.Add($"Missing group referenced in {dr.TableName} -- create it in Settings > Ecommerce before re-running");
                }
                else if (error.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                {
                    advice.Add($"Duplicate key in {dr.TableName} -- check NameColumn uniqueness in source data");
                }
                else if (!string.IsNullOrWhiteSpace(error))
                {
                    advice.Add($"Error in {dr.TableName}: {error}");
                }
            }
        }

        // Also check top-level orchestrator errors
        foreach (var error in result.Errors)
        {
            if (error.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
            {
                advice.Add("FK constraint failed -- check that parent tables are deserialized first (verify predicate ordering)");
            }
        }

        if (hasAnyFailed)
        {
            advice.Add("Re-run deserialization after fixing errors -- successfully applied rows will be skipped (source-wins idempotency)");
        }

        return advice.Distinct().ToList();
    }
}
