namespace costats.Application.Pricing;

public sealed record ModelPricing(
    string ModelId,
    string Provider,
    decimal InputUsdPerToken,
    decimal CachedInputUsdPerToken,
    decimal CacheWriteUsdPerToken,
    decimal OutputUsdPerToken,
    PricingSource Source)
{
    public int? TierThreshold { get; init; }
    public decimal? InputUsdPerTokenAboveTier { get; init; }
    public decimal? CachedInputUsdPerTokenAboveTier { get; init; }
    public decimal? CacheWriteUsdPerTokenAboveTier { get; init; }
    public decimal? OutputUsdPerTokenAboveTier { get; init; }
}
