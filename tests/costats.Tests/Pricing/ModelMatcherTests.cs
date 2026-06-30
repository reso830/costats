using costats.Application.Pricing;
using Xunit;

namespace costats.Tests.Pricing;

public sealed class ModelMatcherTests
{
    [Fact]
    public void Match_StripsProviderPrefixAndCodexSuffix()
    {
        var catalog = new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelPricing("gpt-5", "openai", 0.00000125m, 0.000000125m, 0m, 0.00001m, PricingSource.Embedded)
        };

        var match = new ModelMatcher().Match("openai/gpt-5-codex", catalog, "openai");

        Assert.NotNull(match);
        Assert.Equal("gpt-5", match.ModelId);
    }

    [Fact]
    public void Match_StripsDateSuffixForClaudeModels()
    {
        var catalog = new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-sonnet-4"] = new ModelPricing("claude-sonnet-4", "anthropic", 0.000003m, 0.0000003m, 0.00000375m, 0.000015m, PricingSource.Embedded)
        };

        var match = new ModelMatcher().Match("anthropic/claude-sonnet-4-20250620", catalog, "anthropic");

        Assert.NotNull(match);
        Assert.Equal("claude-sonnet-4", match.ModelId);
    }
}
