using costats.Application.Pricing;
using costats.Core.Pulse;
using Xunit;

namespace costats.Tests.Pricing;

public sealed class PricingCostCalculatorTests
{
    [Fact]
    public void ComputeCost_IncludesInputCacheWriteAndOutputTokens()
    {
        var pricing = new ModelPricing(
            ModelId: "example-model",
            Provider: "example",
            InputUsdPerToken: 1m,
            CachedInputUsdPerToken: 2m,
            CacheWriteUsdPerToken: 3m,
            OutputUsdPerToken: 4m,
            Source: PricingSource.Embedded);

        var ledger = new TokenLedger
        {
            StandardInput = 1,
            CachedInput = 2,
            CacheWriteInput = 3,
            GeneratedOutput = 4
        };

        var cost = PricingCostCalculator.ComputeCost(pricing, ledger);

        Assert.Equal(30m, cost);
    }

    [Fact]
    public void ComputeCost_AppliesTieredRatesAboveThreshold()
    {
        var pricing = new ModelPricing(
            ModelId: "example-model",
            Provider: "example",
            InputUsdPerToken: 1m,
            CachedInputUsdPerToken: 2m,
            CacheWriteUsdPerToken: 3m,
            OutputUsdPerToken: 4m,
            Source: PricingSource.Embedded)
        {
            TierThreshold = 10,
            InputUsdPerTokenAboveTier = 10m,
            CachedInputUsdPerTokenAboveTier = 20m,
            CacheWriteUsdPerTokenAboveTier = 30m,
            OutputUsdPerTokenAboveTier = 40m
        };

        var ledger = new TokenLedger
        {
            StandardInput = 12,
            CachedInput = 12,
            CacheWriteInput = 12,
            GeneratedOutput = 12
        };

        var cost = PricingCostCalculator.ComputeCost(pricing, ledger);

        Assert.Equal(300m, cost);
    }
}
