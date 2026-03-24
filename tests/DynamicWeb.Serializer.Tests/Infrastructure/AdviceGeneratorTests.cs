using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Providers;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

public class AdviceGeneratorTests
{
    [Fact]
    public void GenerateAdvice_ForeignKeyError_ReturnsFkAdvice()
    {
        var result = new OrchestratorResult
        {
            DeserializeResults = new List<ProviderDeserializeResult>
            {
                new()
                {
                    TableName = "EcomOrderStates",
                    Failed = 1,
                    Errors = new[] { "INSERT failed: FOREIGN KEY constraint violation on EcomOrderStates" }
                }
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
            DeserializeResults = new List<ProviderDeserializeResult>
            {
                new()
                {
                    TableName = "EcomProducts",
                    Failed = 1,
                    Errors = new[] { "group 'Default' not found in EcomProducts" }
                }
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
            DeserializeResults = new List<ProviderDeserializeResult>
            {
                new()
                {
                    TableName = "EcomCountries",
                    Failed = 1,
                    Errors = new[] { "duplicate key value in EcomCountries" }
                }
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
            DeserializeResults = new List<ProviderDeserializeResult>
            {
                new()
                {
                    TableName = "EcomOrderStates",
                    Failed = 2,
                    Errors = new[] { "Some error" }
                }
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
            DeserializeResults = new List<ProviderDeserializeResult>
            {
                new()
                {
                    TableName = "EcomCountries",
                    Created = 5,
                    Updated = 2
                }
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
            DeserializeResults = new List<ProviderDeserializeResult>
            {
                new()
                {
                    TableName = "EcomOrderStates",
                    Failed = 1,
                    Errors = new[] { "FOREIGN KEY constraint on EcomOrderStates" }
                },
                new()
                {
                    TableName = "EcomOrderStates",
                    Failed = 1,
                    Errors = new[] { "FOREIGN KEY constraint on EcomOrderStates" }
                }
            }
        };

        var advice = AdviceGenerator.GenerateAdvice(result);
        var fkAdvice = advice.Where(a => a.Contains("FK constraint")).ToList();

        // Should be deduplicated
        Assert.Single(fkAdvice);
    }
}
