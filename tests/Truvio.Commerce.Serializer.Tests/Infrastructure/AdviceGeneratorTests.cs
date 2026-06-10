using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Providers;
using Truvio.Commerce.Serializer.Reporting;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Infrastructure;

/// <summary>
/// Phase 44 / IN-01 (D-10): tests migrated from the deleted
/// <c>OrchestratorResult.DeserializeResults</c> input to the new
/// <c>IReadOnlyList&lt;EntryOutcome&gt;</c> input. Public advice-text contract preserved
/// verbatim — the previous per-table anchor (<c>r.TableName</c>) becomes
/// <c>o.EntryId</c>, which is semantically the same per-entry diagnostic context.
/// </summary>
public class AdviceGeneratorTests
{
    private static EntryOutcome MakeFailedOutcome(string entryId, string error, int failed = 1) =>
        new()
        {
            EntryId = entryId,
            ProviderType = "SqlTable",
            Status = EntryStatus.Failed,
            Message = "Failed",
            Errors = new[] { error },
            Counts = new ProviderCounts(0, 0, 0, failed),
            Duration = TimeSpan.Zero
        };

    private static EntryOutcome MakeSucceededOutcome(string entryId, int created = 5, int updated = 2) =>
        new()
        {
            EntryId = entryId,
            ProviderType = "SqlTable",
            Status = EntryStatus.Succeeded,
            Message = "OK",
            Errors = Array.Empty<string>(),
            Counts = new ProviderCounts(created, updated, 0, 0),
            Duration = TimeSpan.Zero
        };

    [Fact]
    public void GenerateAdvice_ForeignKeyError_ReturnsFkAdvice()
    {
        var result = new OrchestratorResult
        {
            EntryOutcomes = new List<EntryOutcome>
            {
                MakeFailedOutcome("EcomOrderStates",
                    "INSERT failed: FOREIGN KEY constraint violation on EcomOrderStates")
            }
        };

        var advice = AdviceGenerator.GenerateAdvice(result);

        Assert.Contains(advice, a => a.Contains("FK constraint") && a.Contains("EcomOrderStates") && a.Contains("predicate ordering"));
    }

    [Fact]
    public void GenerateAdvice_NotFoundGroupError_ReturnsMissingGroupAdvice()
    {
        var result = new OrchestratorResult
        {
            EntryOutcomes = new List<EntryOutcome>
            {
                MakeFailedOutcome("EcomProducts", "group 'Default' not found in EcomProducts")
            }
        };

        var advice = AdviceGenerator.GenerateAdvice(result);

        Assert.Contains(advice, a => a.Contains("Missing group") && a.Contains("EcomProducts") && a.Contains("Settings > Ecommerce"));
    }

    [Fact]
    public void GenerateAdvice_DuplicateError_ReturnsDuplicateAdvice()
    {
        var result = new OrchestratorResult
        {
            EntryOutcomes = new List<EntryOutcome>
            {
                MakeFailedOutcome("EcomCountries", "duplicate key value in EcomCountries")
            }
        };

        var advice = AdviceGenerator.GenerateAdvice(result);

        Assert.Contains(advice, a => a.Contains("Duplicate key") && a.Contains("EcomCountries") && a.Contains("NameColumn"));
    }

    [Fact]
    public void GenerateAdvice_AnyFailedRows_AddsRerunAdvice()
    {
        var result = new OrchestratorResult
        {
            EntryOutcomes = new List<EntryOutcome>
            {
                MakeFailedOutcome("EcomOrderStates", "Some error", failed: 2)
            }
        };

        var advice = AdviceGenerator.GenerateAdvice(result);

        Assert.Contains(advice, a => a.Contains("Re-run deserialization") && a.Contains("idempotency"));
    }

    [Fact]
    public void GenerateAdvice_NoErrors_ReturnsEmptyList()
    {
        var result = new OrchestratorResult
        {
            EntryOutcomes = new List<EntryOutcome>
            {
                MakeSucceededOutcome("EcomCountries", created: 5, updated: 2)
            }
        };

        var advice = AdviceGenerator.GenerateAdvice(result);

        Assert.Empty(advice);
    }

    [Fact]
    public void GenerateAdvice_DeduplicatesIdenticalAdvice()
    {
        var result = new OrchestratorResult
        {
            EntryOutcomes = new List<EntryOutcome>
            {
                MakeFailedOutcome("EcomOrderStates", "FOREIGN KEY constraint on EcomOrderStates"),
                MakeFailedOutcome("EcomOrderStates", "FOREIGN KEY constraint on EcomOrderStates")
            }
        };

        var advice = AdviceGenerator.GenerateAdvice(result);
        var fkAdvice = advice.Where(a => a.Contains("FK constraint")).ToList();

        // Should be deduplicated
        Assert.Single(fkAdvice);
    }
}
