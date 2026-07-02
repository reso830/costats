using costats.Application.Pricing;

namespace costats.Infrastructure.Pricing;

public sealed class EmbeddedPricingCatalog : IPricingCatalog
{
    private static readonly IReadOnlyDictionary<string, ModelPricing> Catalog =
        new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-haiku-4-5"] = Anthropic("claude-haiku-4-5", 0.000001m, 0.0000001m, 0.00000125m, 0.000005m),
            ["claude-sonnet-4-5"] = Anthropic("claude-sonnet-4-5", 0.000003m, 0.0000003m, 0.00000375m, 0.000015m) with
            {
                TierThreshold = 200_000,
                InputUsdPerTokenAboveTier = 0.000006m,
                CachedInputUsdPerTokenAboveTier = 0.0000006m,
                CacheWriteUsdPerTokenAboveTier = 0.0000075m,
                OutputUsdPerTokenAboveTier = 0.0000225m
            },
            ["claude-opus-4-5"] = Anthropic("claude-opus-4-5", 0.000005m, 0.0000005m, 0.00000625m, 0.000025m),
            ["claude-opus-4"] = Anthropic("claude-opus-4", 0.000015m, 0.0000015m, 0.00001875m, 0.000075m),
            ["claude-sonnet-4"] = Anthropic("claude-sonnet-4", 0.000003m, 0.0000003m, 0.00000375m, 0.000015m),
            ["gemini-3.5-flash"] = Google("gemini-3.5-flash", 0.000000075m, 0.00000001875m, 0.0000003m),
            ["gemini-3.5-pro"] = Google("gemini-3.5-pro", 0.00000125m, 0.0000003125m, 0.000005m),
            ["gpt-5"] = OpenAi("gpt-5", 0.00000125m, 0.000000125m, 0.00001m),
            ["gpt-5.2"] = OpenAi("gpt-5.2", 0.00000175m, 0.000000175m, 0.000014m),
            ["o3"] = OpenAi("o3", 0.00001m, 0.0000025m, 0.00004m),
            ["o4-mini"] = OpenAi("o4-mini", 0.0000011m, 0.000000275m, 0.0000044m)
        };

    private readonly ModelMatcher _matcher = new();

    public Task<ModelPricing?> LookupAsync(string modelId, string? providerHint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_matcher.Match(modelId, Catalog, providerHint)?.Pricing);
    }

    private static ModelPricing Anthropic(
        string modelId,
        decimal inputUsdPerToken,
        decimal cachedInputUsdPerToken,
        decimal cacheWriteUsdPerToken,
        decimal outputUsdPerToken) =>
        new(
            modelId,
            "anthropic",
            inputUsdPerToken,
            cachedInputUsdPerToken,
            cacheWriteUsdPerToken,
            outputUsdPerToken,
            PricingSource.Embedded);

    private static ModelPricing OpenAi(
        string modelId,
        decimal inputUsdPerToken,
        decimal cachedInputUsdPerToken,
        decimal outputUsdPerToken) =>
        new(
            modelId,
            "openai",
            inputUsdPerToken,
            cachedInputUsdPerToken,
            0m,
            outputUsdPerToken,
            PricingSource.Embedded);

    private static ModelPricing Google(
        string modelId,
        decimal inputUsdPerToken,
        decimal cachedInputUsdPerToken,
        decimal outputUsdPerToken) =>
        new(
            modelId,
            "google",
            inputUsdPerToken,
            cachedInputUsdPerToken,
            0m,
            outputUsdPerToken,
            PricingSource.Embedded);
}
