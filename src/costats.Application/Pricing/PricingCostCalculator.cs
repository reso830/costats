using costats.Core.Pulse;

namespace costats.Application.Pricing;

public static class PricingCostCalculator
{
    public static decimal ComputeCost(ModelPricing pricing, TokenLedger ledger) =>
        ComputeTieredCost(ledger.StandardInput, pricing.InputUsdPerToken, pricing.InputUsdPerTokenAboveTier, pricing.TierThreshold) +
        ComputeTieredCost(ledger.CachedInput, pricing.CachedInputUsdPerToken, pricing.CachedInputUsdPerTokenAboveTier, pricing.TierThreshold) +
        ComputeTieredCost(ledger.CacheWriteInput, pricing.CacheWriteUsdPerToken, pricing.CacheWriteUsdPerTokenAboveTier, pricing.TierThreshold) +
        ComputeTieredCost(ledger.GeneratedOutput, pricing.OutputUsdPerToken, pricing.OutputUsdPerTokenAboveTier, pricing.TierThreshold);

    private static decimal ComputeTieredCost(int tokens, decimal baseRate, decimal? aboveTierRate, int? tierThreshold)
    {
        if (tokens <= 0)
        {
            return 0m;
        }

        if (tierThreshold is not { } threshold || aboveTierRate is not { } aboveRate)
        {
            return tokens * baseRate;
        }

        var belowTier = Math.Min(tokens, threshold);
        var aboveTier = Math.Max(0, tokens - threshold);

        return (belowTier * baseRate) + (aboveTier * aboveRate);
    }
}
