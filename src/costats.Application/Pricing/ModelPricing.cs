namespace costats.Application.Pricing;

public sealed record ModelPricing(
    string ModelId,
    string Provider,
    decimal InputUsdPerToken,
    decimal CachedInputUsdPerToken,
    decimal CacheWriteUsdPerToken,
    decimal OutputUsdPerToken,
    PricingSource Source);
