using costats.Application.Pricing;
using costats.Application.Pulse;
using costats.Application.Settings;
using costats.Core.Pulse;
using costats.Infrastructure.Expense;

namespace costats.Infrastructure.Providers;

public sealed class AntigravitySource : ISignalSource
{
    private const int RollingWindowDays = 30;

    private readonly AppSettings _settings;
    private readonly IPricingCatalog _pricingCatalog;

    public AntigravitySource(AppSettings settings, IPricingCatalog pricingCatalog)
    {
        _settings = settings;
        _pricingCatalog = pricingCatalog;
    }

    public ProviderProfile Profile => ProviderCatalog.Antigravity;

    public async Task<ProviderReading> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        if (!_settings.AntigravityTelemetryEnabled || _settings.AntigravityTelemetryRoots.Length == 0)
        {
            return new ProviderReading(
                Usage: null,
                Identity: null,
                StatusSummary: "Antigravity telemetry disabled or no roots configured",
                CapturedAt: now,
                Confidence: ReadingConfidence.Low,
                Source: ReadingSource.LocalLog);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var windowStart = today.AddDays(-(RollingWindowDays - 1));
        var slices = await AntigravityTelemetryDigestor
            .DigestAsync(_pricingCatalog, _settings.AntigravityTelemetryRoots, windowStart, today, cancellationToken)
            .ConfigureAwait(false);

        if (slices.Count == 0)
        {
            return new ProviderReading(
                Usage: null,
                Identity: null,
                StatusSummary: "No Antigravity usage data found",
                CapturedAt: now,
                Confidence: ReadingConfidence.Low,
                Source: ReadingSource.LocalLog);
        }

        var todaySlices = slices.Where(s => s.Period == today).ToList();
        var todayTokens = todaySlices.Aggregate(TokenLedger.Empty, (acc, s) => acc.Combine(s.Tokens));
        var todayCost = todaySlices.Sum(s => s.ComputedCostUsd);
        var windowTokens = slices.Aggregate(TokenLedger.Empty, (acc, s) => acc.Combine(s.Tokens));
        var windowCost = slices.Sum(s => s.ComputedCostUsd);

        var consumption = new ConsumptionDigest
        {
            TodayTokens = todayTokens,
            TodayCostUsd = todayCost,
            RollingWindowTokens = windowTokens,
            RollingWindowCostUsd = windowCost,
            RollingWindowDays = RollingWindowDays,
            DailyBreakdown = slices,
            ComputedAt = now
        };

        var usage = new UsagePulse(
            ProviderId: Profile.ProviderId,
            CapturedAt: now,
            SessionUsed: null,
            SessionLimit: null,
            WeekUsed: null,
            WeekLimit: null,
            SpendingBucket: null,
            Consumption: consumption,
            SessionWindow: null,
            WeekWindow: null);

        return new ProviderReading(
            Usage: usage,
            Identity: new IdentityCard(Profile.ProviderId, Profile.DisplayName, null, null, "Flash/Pro", "Local DB"),
            StatusSummary: $"Updated {now.ToLocalTime():t}",
            CapturedAt: usage.CapturedAt,
            Confidence: ReadingConfidence.Medium,
            Source: ReadingSource.LocalLog);
    }
}
