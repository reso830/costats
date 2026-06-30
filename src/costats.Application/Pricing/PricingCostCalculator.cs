using costats.Core.Pulse;

namespace costats.Application.Pricing;

public static class PricingCostCalculator
{
    public static decimal ComputeCost(ModelPricing pricing, TokenLedger ledger) =>
        (ledger.StandardInput * pricing.InputUsdPerToken) +
        (ledger.CachedInput * pricing.CachedInputUsdPerToken) +
        (ledger.CacheWriteInput * pricing.CacheWriteUsdPerToken) +
        (ledger.GeneratedOutput * pricing.OutputUsdPerToken);
}
