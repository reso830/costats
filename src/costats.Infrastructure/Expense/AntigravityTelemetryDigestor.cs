using costats.Application.Pricing;
using costats.Core.Pulse;
using costats.Infrastructure.Usage;
using Microsoft.Data.Sqlite;

namespace costats.Infrastructure.Expense;

public static class AntigravityTelemetryDigestor
{
    public static async Task<IReadOnlyList<ConsumptionSlice>> DigestAsync(
        IPricingCatalog pricingCatalog,
        IEnumerable<string> roots,
        DateOnly since,
        DateOnly until,
        CancellationToken cancellationToken = default)
    {
        var aggregates = new Dictionary<DateOnly, Dictionary<string, ConsumptionSliceAggregator.SliceAccumulator>>();

        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var dbFile in Directory.EnumerateFiles(root, "*.db"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DigestDbFileAsync(pricingCatalog, dbFile, since, until, aggregates, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return ConsumptionSliceAggregator.Build(aggregates);
    }

    private static async Task DigestDbFileAsync(
        IPricingCatalog pricingCatalog,
        string dbFile,
        DateOnly since,
        DateOnly until,
        Dictionary<DateOnly, Dictionary<string, ConsumptionSliceAggregator.SliceAccumulator>> aggregates,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = $"Data Source={dbFile};Mode=ReadOnly;Cache=Shared;Pooling=False;";
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using (var checkCommand = new SqliteCommand(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='steps';",
                connection))
            {
                var tableExists = await checkCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (tableExists is null)
                {
                    return;
                }
            }

            using var command = new SqliteCommand("SELECT metadata FROM steps WHERE metadata IS NOT NULL;", connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (reader["metadata"] is not byte[] metadata ||
                    !TryReadTelemetryRecord(metadata, out var record) ||
                    record.Tokens.TotalConsumed == 0)
                {
                    continue;
                }

                var period = DateOnly.FromDateTime(record.Timestamp.LocalDateTime);
                if (period < since || period > until)
                {
                    continue;
                }

                var pricing = await pricingCatalog.LookupAsync(record.Model, "google", cancellationToken).ConfigureAwait(false);
                var cost = pricing is null ? 0m : PricingCostCalculator.ComputeCost(pricing, record.Tokens);
                ConsumptionSliceAggregator.Add(aggregates, period, record.Model, record.Tokens, cost);
            }
        }
        catch (IOException)
        {
        }
        catch (SqliteException)
        {
        }
    }

    private static bool TryReadTelemetryRecord(byte[] metadata, out AntigravityTelemetryRecord record)
    {
        record = default;
        var fields = ProtobufParser.Parse(metadata);

        if (!TryReadTimestamp(fields, out var timestamp) ||
            !TryReadUsage(fields, out var model, out var ledger))
        {
            return false;
        }

        record = new AntigravityTelemetryRecord(model, timestamp, ledger);
        return true;
    }

    private static bool TryReadTimestamp(
        IReadOnlyList<(int FieldNumber, object Value)> fields,
        out DateTimeOffset timestamp)
    {
        timestamp = default;
        foreach (var field in fields)
        {
            if (field is { FieldNumber: 1, Value: byte[] timeBytes })
            {
                foreach (var timeField in ProtobufParser.Parse(timeBytes))
                {
                    if (timeField is { FieldNumber: 1, Value: long seconds } && seconds > 0)
                    {
                        timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryReadUsage(
        IReadOnlyList<(int FieldNumber, object Value)> fields,
        out string model,
        out TokenLedger ledger)
    {
        model = "gemini-3.5-flash";
        ledger = TokenLedger.Empty;

        foreach (var field in fields)
        {
            if (field is not { FieldNumber: 9, Value: byte[] usageBytes })
            {
                continue;
            }

            long input = 0;
            long output = 0;
            long cached = 0;
            long modelIdCode = 0;

            foreach (var usageField in ProtobufParser.Parse(usageBytes))
            {
                switch (usageField)
                {
                    case { FieldNumber: 1, Value: long value }:
                        modelIdCode = value;
                        break;
                    case { FieldNumber: 2, Value: long value }:
                        input = value;
                        break;
                    case { FieldNumber: 3, Value: long value }:
                        output = value;
                        break;
                    case { FieldNumber: 5, Value: long value }:
                        cached = value;
                        break;
                }
            }

            model = modelIdCode switch
            {
                1050 => "gemini-3.5-pro",
                1133 => "gemini-3.5-flash",
                _ => "gemini-3.5-flash"
            };

            ledger = new TokenLedger
            {
                StandardInput = ToTokenCount(input - cached),
                CachedInput = ToTokenCount(cached),
                CacheWriteInput = 0,
                GeneratedOutput = ToTokenCount(output)
            };
            return true;
        }

        return false;
    }

    private static int ToTokenCount(long value) => value <= 0 ? 0 : value > int.MaxValue ? int.MaxValue : (int)value;

    private readonly record struct AntigravityTelemetryRecord(
        string Model,
        DateTimeOffset Timestamp,
        TokenLedger Tokens);
}
