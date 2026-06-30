using costats.Infrastructure.Expense;
using costats.Infrastructure.Pricing;
using Xunit;

namespace costats.Tests.Expense;

public sealed class CopilotTelemetryDigestorTests
{
    [Fact]
    public async Task DigestAsync_ExtractsOnlyModelTimestampAndTokens()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "events.jsonl");
            await File.WriteAllTextAsync(file, """
{"timestamp":"2026-06-29T01:02:03Z","model":"gpt-5","prompt":"secret prompt text","completion":"secret completion text","input_tokens":10,"cached_input_tokens":2,"output_tokens":5,"reasoning_tokens":1}
""");

            var slices = await CopilotTelemetryDigestor.DigestAsync(
                new EmbeddedPricingCatalog(),
                [root],
                new DateOnly(2026, 06, 28),
                new DateOnly(2026, 06, 30),
                CancellationToken.None);

            Assert.Single(slices);
            Assert.Equal(new DateOnly(2026, 06, 29), slices[0].Period);
            Assert.Equal("gpt-5", slices[0].ModelIdentifier);
            Assert.Equal(10, slices[0].Tokens.StandardInput + slices[0].Tokens.CachedInput);
            Assert.Equal(5, slices[0].Tokens.GeneratedOutput);
            Assert.True(slices[0].ComputedCostUsd > 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DigestAsync_CountsReasoningOnlyTokensAsGeneratedOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "reasoning.jsonl");
            await File.WriteAllTextAsync(file, """
{"timestamp":"2026-06-29T01:02:03Z","model":"o4-mini","input_tokens":10,"reasoning_tokens":7}
""");

            var slices = await CopilotTelemetryDigestor.DigestAsync(
                new EmbeddedPricingCatalog(),
                [root],
                new DateOnly(2026, 06, 28),
                new DateOnly(2026, 06, 30),
                CancellationToken.None);

            Assert.Single(slices);
            Assert.Equal(7, slices[0].Tokens.GeneratedOutput);
            Assert.True(slices[0].ComputedCostUsd > 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
