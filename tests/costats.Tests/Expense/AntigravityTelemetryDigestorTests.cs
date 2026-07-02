using Microsoft.Data.Sqlite;
using costats.Infrastructure.Expense;
using costats.Infrastructure.Pricing;
using costats.Infrastructure.Usage;
using Xunit;

namespace costats.Tests.Expense;

public sealed class AntigravityTelemetryDigestorTests
{
    [Fact]
    public void ProtobufParser_DecodesVarintFields()
    {
        byte[] raw = [0x0A, 0x0C, 0x08, 0xcd, 0xc4, 0xdf, 0xd1, 0x06, 0x10, 0x84, 0xc1, 0x87, 0x82, 0x03];

        var fields = ProtobufParser.Parse(raw);

        Assert.Single(fields);
        Assert.Equal(1, fields[0].FieldNumber);

        var submessage = (byte[])fields[0].Value;
        var subFields = ProtobufParser.Parse(submessage);

        Assert.Equal(1, subFields[0].FieldNumber);
        Assert.Equal(1782047309L, (long)subFields[0].Value);
        Assert.Equal(2, subFields[1].FieldNumber);
        Assert.Equal(809623684L, (long)subFields[1].Value);
    }

    [Fact]
    public async Task DigestAsync_ExtractsTokensAndCostFromSQLiteWAL()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "conversation_test.db");

        try
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False;"))
            {
                conn.Open();
                using (var cmd = new SqliteCommand("CREATE TABLE steps (idx INTEGER PRIMARY KEY, metadata BLOB);", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                byte[] usageSubmessage =
                [
                    0x08, 0x9a, 0x08,
                    0x10, 0xf6, 0xe9, 0x01,
                    0x18, 0xad, 0x02,
                    0x28, 0xea, 0xbf, 0x01
                ];

                var metadataList = new List<byte>();
                metadataList.Add(0x0A);
                metadataList.Add(12);
                metadataList.AddRange([0x08, 0xcd, 0xc4, 0xdf, 0xd1, 0x06, 0x10, 0x84, 0xc1, 0x87, 0x82, 0x03]);
                metadataList.Add(0x4A);
                metadataList.Add((byte)usageSubmessage.Length);
                metadataList.AddRange(usageSubmessage);

                using var insertCmd = new SqliteCommand("INSERT INTO steps (idx, metadata) VALUES (1, @metadata);", conn);
                insertCmd.Parameters.AddWithValue("@metadata", metadataList.ToArray());
                insertCmd.ExecuteNonQuery();
            }

            var slices = await AntigravityTelemetryDigestor.DigestAsync(
                new EmbeddedPricingCatalog(),
                [tempDir],
                new DateOnly(2026, 06, 20),
                new DateOnly(2026, 06, 22),
                CancellationToken.None);

            Assert.Single(slices);
            var slice = slices[0];
            Assert.Equal(new DateOnly(2026, 06, 21), slice.Period);
            Assert.Equal("gemini-3.5-pro", slice.ModelIdentifier);
            Assert.Equal(5388, slice.Tokens.StandardInput);
            Assert.Equal(24554, slice.Tokens.CachedInput);
            Assert.Equal(301, slice.Tokens.GeneratedOutput);
            Assert.True(slice.ComputedCostUsd > 0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DigestAsync_SkipsInaccessibleOrCorruptFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "corrupt_test.db");

        try
        {
            await File.WriteAllTextAsync(dbPath, "Not a SQLite database file");

            var slices = await AntigravityTelemetryDigestor.DigestAsync(
                new EmbeddedPricingCatalog(),
                [tempDir],
                new DateOnly(2026, 06, 20),
                new DateOnly(2026, 06, 22),
                CancellationToken.None);

            Assert.Empty(slices);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
