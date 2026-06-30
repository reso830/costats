using costats.Infrastructure.Pricing;
using Xunit;

namespace costats.Tests.Pricing;

public sealed class EmbeddedPricingCatalogTests
{
    [Fact]
    public async Task LookupAsync_MatchesOpenAiCodexModel()
    {
        var pricing = await new EmbeddedPricingCatalog().LookupAsync("openai/gpt-5-codex", "openai", CancellationToken.None);

        Assert.NotNull(pricing);
        Assert.Equal("gpt-5", pricing.ModelId);
    }

    [Fact]
    public async Task LookupAsync_MatchesClaudeDateVersion()
    {
        var pricing = await new EmbeddedPricingCatalog().LookupAsync("anthropic/claude-sonnet-4-20250620", "anthropic", CancellationToken.None);

        Assert.NotNull(pricing);
        Assert.Equal("claude-sonnet-4", pricing.ModelId);
    }
}
