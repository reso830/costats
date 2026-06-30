using costats.Core.Pulse;

namespace costats.Infrastructure.Expense;

internal static class ConsumptionSliceAggregator
{
    public static void Add(
        Dictionary<DateOnly, Dictionary<string, SliceAccumulator>> aggregates,
        DateOnly period,
        string modelIdentifier,
        TokenLedger ledger,
        decimal cost)
    {
        if (!aggregates.TryGetValue(period, out var byModel))
        {
            byModel = new Dictionary<string, SliceAccumulator>(StringComparer.OrdinalIgnoreCase);
            aggregates[period] = byModel;
        }

        if (!byModel.TryGetValue(modelIdentifier, out var accumulator))
        {
            accumulator = new SliceAccumulator();
            byModel[modelIdentifier] = accumulator;
        }

        accumulator.StandardInput += ledger.StandardInput;
        accumulator.CachedInput += ledger.CachedInput;
        accumulator.CacheWriteInput += ledger.CacheWriteInput;
        accumulator.GeneratedOutput += ledger.GeneratedOutput;
        accumulator.Cost += cost;
    }

    public static IReadOnlyList<ConsumptionSlice> Build(
        Dictionary<DateOnly, Dictionary<string, SliceAccumulator>> aggregates) =>
        aggregates
            .SelectMany(day => day.Value.Select(model =>
            {
                var accumulator = model.Value;
                return new ConsumptionSlice
                {
                    Period = day.Key,
                    ModelIdentifier = model.Key,
                    Tokens = accumulator.ToTokenLedger(),
                    ComputedCostUsd = accumulator.Cost
                };
            }))
            .OrderByDescending(slice => slice.Period)
            .ThenBy(slice => slice.ModelIdentifier, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal sealed class SliceAccumulator
    {
        public long StandardInput { get; set; }
        public long CachedInput { get; set; }
        public long CacheWriteInput { get; set; }
        public long GeneratedOutput { get; set; }
        public decimal Cost { get; set; }

        public TokenLedger ToTokenLedger() => new()
        {
            StandardInput = ClampToInt(StandardInput),
            CachedInput = ClampToInt(CachedInput),
            CacheWriteInput = ClampToInt(CacheWriteInput),
            GeneratedOutput = ClampToInt(GeneratedOutput)
        };

        private static int ClampToInt(long value)
        {
            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (value < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)value;
        }
    }
}
