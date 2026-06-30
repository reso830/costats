# Provider Pricing and GHCP Groundwork Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the useful parts of RileyCornelius' fork into this fork as cleaner provider-expansion groundwork, with GitHub Copilot usage support and explicit privacy/security controls.

**Architecture:** Keep the existing `ISignalSource` ingestion contract, but extract pricing/model matching into tested application services and add a local-only GitHub Copilot telemetry digestor. Defer cosmetic UI changes; only change UI/view-model code where the current hard-coded provider assumptions block additional providers.

**Tech Stack:** .NET `net10.0-windows`, WPF, `Microsoft.Extensions.Hosting`, existing `ISignalSource`/`PulseState` pipeline, xUnit tests added under `tests/costats.Tests`.

---

## Scope Decisions

- Port Riley's pricing/model matching ideas, not the exact patch. Keep the result smaller and local-first.
- Do not port Riley's dark theme or other cosmetic UI changes.
- Do not enable automatic background downloads of pricing catalogs in this phase.
- Add GHCP usage as GitHub Copilot local telemetry/log digestion. Keep the existing token-backed Copilot API source intact.
- Make GHCP local telemetry disabled by default and opt-in through settings.
- Do not read prompts, completions, source code, chat text, or editor workspace files. Parse only JSON/JSONL/log records that expose model, timestamp, and token counts.

## Privacy and Security Implications

The risky area is GHCP local telemetry scanning. A digestor that recursively scans editor global storage could accidentally encounter prompt/chat payloads or unrelated extension data. The implementation must constrain what is read and what is retained:

- Opt-in: `AppSettings.CopilotTelemetryEnabled` defaults to `false`.
- Path control: default roots are known Copilot extension storage directories, but user-provided roots must be explicit absolute paths.
- Data minimization: parsed records keep only `model`, `timestamp`, and token counters.
- No exfiltration: GHCP telemetry data is never sent over the network.
- No raw persistence: do not write raw telemetry lines to costats settings, logs, or snapshots.
- Error behavior: unreadable files are skipped; file paths may be logged at debug level only if needed, never file content.
- Network pricing refresh is not implemented in this phase. Pricing uses an embedded local snapshot.

## File Structure

- Create `tests/costats.Tests/costats.Tests.csproj`: test project for pricing, matching, and GHCP digesting.
- Modify `Directory.Packages.props`: add centralized versions for xUnit and test SDK packages.
- Modify `costats.sln`: include the test project.
- Create `src/costats.Application/Pricing/ModelPricing.cs`: provider/model pricing record.
- Create `src/costats.Application/Pricing/PricingSource.cs`: source metadata enum.
- Create `src/costats.Application/Pricing/IPricingCatalog.cs`: lookup abstraction.
- Create `src/costats.Application/Pricing/ModelMatcher.cs`: provider-aware model normalization and matching.
- Create `src/costats.Application/Pricing/PricingCostCalculator.cs`: cost calculation for `TokenLedger`.
- Create `src/costats.Application/Pricing/PricingOptions.cs`: future-proof pricing settings with network refresh disabled by default.
- Modify `src/costats.Core/Pulse/RateCard.cs`: keep compatibility wrappers or move static tariff lookups behind the new catalog.
- Create `src/costats.Infrastructure/Pricing/EmbeddedPricingCatalog.cs`: in-process catalog backed by static local data.
- Create `src/costats.Infrastructure/Expense/ConsumptionSliceAggregator.cs`: aggregation helper shared by log digestors.
- Create `src/costats.Infrastructure/Expense/CopilotTelemetryDigestor.cs`: privacy-constrained GHCP local telemetry parser.
- Modify `src/costats.Infrastructure/Expense/ExpenseAnalyzer.cs`: add optional Copilot telemetry aggregation entrypoint.
- Modify `src/costats.Infrastructure/Providers/CopilotPersonalSource.cs`: attach telemetry-derived consumption only when enabled.
- Modify `src/costats.Application/Settings/AppSettings.cs`: add opt-in Copilot telemetry settings.
- Modify `src/costats.App/App.xaml.cs`: register pricing catalog and pass settings into Copilot source dependencies.
- Modify `src/costats.App/ViewModels/PulseViewModel.cs`: reduce hard-coded provider assumptions only enough to avoid dropping new `copilot:*` or future provider IDs.
- Create `tests/costats.Tests/Pricing/ModelMatcherTests.cs`.
- Create `tests/costats.Tests/Pricing/PricingCostCalculatorTests.cs`.
- Create `tests/costats.Tests/Expense/CopilotTelemetryDigestorTests.cs`.
- Create `docs/COPILOT-TELEMETRY.md`: document exactly what GHCP telemetry reads and how to disable it.

---

### Task 1: Add Test Project

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `costats.sln`
- Create: `tests/costats.Tests/costats.Tests.csproj`

- [x] **Step 1: Add package versions**

Add these entries inside the existing `<ItemGroup>` in `Directory.Packages.props`:

```xml
<PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
<PackageVersion Include="xunit" Version="2.9.2" />
<PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageVersion Include="coverlet.collector" Version="6.0.2" />
```

- [x] **Step 2: Create the test project file**

Create `tests/costats.Tests/costats.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\costats.Core\costats.Core.csproj" />
    <ProjectReference Include="..\..\src\costats.Application\costats.Application.csproj" />
    <ProjectReference Include="..\..\src\costats.Infrastructure\costats.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

- [x] **Step 3: Add the project to the solution**

Run:

```powershell
dotnet sln .\costats.sln add .\tests\costats.Tests\costats.Tests.csproj
```

Expected: solution reports that the project was added.

- [x] **Step 4: Verify test discovery**

Run:

```powershell
dotnet test .\costats.sln --no-restore
```

Expected: either zero tests discovered or a successful build after restore is available. If restore is required, run `dotnet test .\costats.sln` during execution.

- [x] **Step 5: Commit**

```powershell
git add Directory.Packages.props costats.sln tests/costats.Tests/costats.Tests.csproj
git commit -m "test: add costats test project"
```

---

### Task 2: Add Pricing Domain Tests

**Files:**
- Create: `tests/costats.Tests/Pricing/ModelMatcherTests.cs`
- Create: `tests/costats.Tests/Pricing/PricingCostCalculatorTests.cs`

- [x] **Step 1: Write failing model matcher tests**

Create `tests/costats.Tests/Pricing/ModelMatcherTests.cs`:

```csharp
using costats.Application.Pricing;

namespace costats.Tests.Pricing;

public sealed class ModelMatcherTests
{
    [Fact]
    public void Match_StripsProviderPrefixAndCodexSuffix()
    {
        var catalog = new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelPricing("gpt-5", "openai", 0.00000125m, 0.000000125m, 0m, 0.00001m, 0m, PricingSource.Embedded)
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
            ["claude-sonnet-4"] = new ModelPricing("claude-sonnet-4", "anthropic", 0.000003m, 0.0000003m, 0.00000375m, 0.000015m, 0m, PricingSource.Embedded)
        };

        var match = new ModelMatcher().Match("anthropic/claude-sonnet-4-20250620", catalog, "anthropic");

        Assert.NotNull(match);
        Assert.Equal("claude-sonnet-4", match.ModelId);
    }
}
```

- [x] **Step 2: Write failing cost calculator tests**

Create `tests/costats.Tests/Pricing/PricingCostCalculatorTests.cs`:

```csharp
using costats.Application.Pricing;
using costats.Core.Pulse;

namespace costats.Tests.Pricing;

public sealed class PricingCostCalculatorTests
{
    [Fact]
    public void ComputeCost_IncludesInputCacheWriteOutputAndReasoningTokens()
    {
        var pricing = new ModelPricing(
            ModelId: "example-model",
            Provider: "example",
            InputUsdPerToken: 1m,
            CachedInputUsdPerToken: 2m,
            CacheWriteUsdPerToken: 3m,
            OutputUsdPerToken: 4m,
            ReasoningOutputUsdPerToken: 5m,
            Source: PricingSource.Embedded);

        var ledger = new TokenLedger
        {
            StandardInput = 1,
            CachedInput = 2,
            CacheWriteInput = 3,
            GeneratedOutput = 4,
            ReasoningOutput = 5
        };

        var cost = PricingCostCalculator.ComputeCost(pricing, ledger);

        Assert.Equal(70m, cost);
    }
}
```

- [x] **Step 3: Run tests and confirm they fail**

Run:

```powershell
dotnet test .\tests\costats.Tests\costats.Tests.csproj --filter "FullyQualifiedName~Pricing"
```

Expected: compile fails because `costats.Application.Pricing` types do not exist.

---

### Task 3: Implement Pricing Domain

**Files:**
- Create: `src/costats.Application/Pricing/ModelPricing.cs`
- Create: `src/costats.Application/Pricing/PricingSource.cs`
- Create: `src/costats.Application/Pricing/IPricingCatalog.cs`
- Create: `src/costats.Application/Pricing/ModelMatcher.cs`
- Create: `src/costats.Application/Pricing/PricingCostCalculator.cs`
- Create: `src/costats.Application/Pricing/PricingOptions.cs`

- [x] **Step 1: Add pricing records and interfaces**

Create `src/costats.Application/Pricing/ModelPricing.cs`:

```csharp
namespace costats.Application.Pricing;

public sealed record ModelPricing(
    string ModelId,
    string Provider,
    decimal InputUsdPerToken,
    decimal CachedInputUsdPerToken,
    decimal CacheWriteUsdPerToken,
    decimal OutputUsdPerToken,
    decimal ReasoningOutputUsdPerToken,
    PricingSource Source);
```

Create `src/costats.Application/Pricing/PricingSource.cs`:

```csharp
namespace costats.Application.Pricing;

public enum PricingSource
{
    Embedded = 0,
    UserConfigured = 1
}
```

Create `src/costats.Application/Pricing/IPricingCatalog.cs`:

```csharp
namespace costats.Application.Pricing;

public interface IPricingCatalog
{
    Task<ModelPricing?> LookupAsync(string modelId, string? providerHint, CancellationToken cancellationToken);
}
```

- [x] **Step 2: Add model matcher**

Create `src/costats.Application/Pricing/ModelMatcher.cs` with provider-prefix stripping for `anthropic/`, `anthropic.`, `openai/`, `azure/`, `bedrock/`, `vertex_ai/`, `openrouter/`, date-suffix stripping for `-\d{8}`, and `-codex` suffix stripping. It must expose:

```csharp
public sealed record ModelMatch(string ModelId, ModelPricing Pricing);

public sealed partial class ModelMatcher
{
    public ModelMatch? Match(
        string modelId,
        IReadOnlyDictionary<string, ModelPricing> catalog,
        string? providerHint = null);
}
```

- [x] **Step 3: Add cost calculator**

Create `src/costats.Application/Pricing/PricingCostCalculator.cs`:

```csharp
using costats.Core.Pulse;

namespace costats.Application.Pricing;

public static class PricingCostCalculator
{
    public static decimal ComputeCost(ModelPricing pricing, TokenLedger ledger) =>
        (ledger.StandardInput * pricing.InputUsdPerToken) +
        (ledger.CachedInput * pricing.CachedInputUsdPerToken) +
        (ledger.CacheWriteInput * pricing.CacheWriteUsdPerToken) +
        (ledger.GeneratedOutput * pricing.OutputUsdPerToken) +
        (ledger.ReasoningOutput * pricing.ReasoningOutputUsdPerToken);
}
```

- [x] **Step 4: Add pricing options**

Create `src/costats.Application/Pricing/PricingOptions.cs`:

```csharp
namespace costats.Application.Pricing;

public sealed class PricingOptions
{
    public bool EnableNetworkRefresh { get; set; } = false;
    public int RefreshIntervalHours { get; set; } = 24;
}
```

- [x] **Step 5: Run pricing tests**

Run:

```powershell
dotnet test .\tests\costats.Tests\costats.Tests.csproj --filter "FullyQualifiedName~Pricing"
```

Expected: pricing tests pass.

- [x] **Step 6: Commit**

```powershell
git add src/costats.Application/Pricing tests/costats.Tests/Pricing
git commit -m "feat: add provider pricing domain"
```

---

### Task 4: Add Embedded Local Pricing Catalog

**Files:**
- Create: `src/costats.Infrastructure/Pricing/EmbeddedPricingCatalog.cs`
- Modify: `src/costats.App/App.xaml.cs`
- Modify: `src/costats.Core/Pulse/RateCard.cs`
- Test: `tests/costats.Tests/Pricing/EmbeddedPricingCatalogTests.cs`

- [x] **Step 1: Write catalog lookup tests**

Create `tests/costats.Tests/Pricing/EmbeddedPricingCatalogTests.cs`:

```csharp
using costats.Infrastructure.Pricing;

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
```

- [x] **Step 2: Implement catalog**

Create `src/costats.Infrastructure/Pricing/EmbeddedPricingCatalog.cs` with a private dictionary containing the current `TariffRegistry` Claude and Codex rates plus Copilot-relevant OpenAI models where known. Use `ModelMatcher` for lookup and return `Task.FromResult(match?.Pricing)`.

- [x] **Step 3: Preserve compatibility**

Modify `src/costats.Core/Pulse/RateCard.cs` only where needed to avoid duplicated behavior breaking existing digestors. Keep `TariffRegistry.FindClaudeRate` and `TariffRegistry.FindCodexRate` callable until all current consumers are moved.

- [x] **Step 4: Register catalog in DI**

In `src/costats.App/App.xaml.cs`, add:

```csharp
services.AddSingleton<IPricingCatalog, EmbeddedPricingCatalog>();
```

Add required `using costats.Application.Pricing;` and `using costats.Infrastructure.Pricing;`.

- [x] **Step 5: Run tests and build**

Run:

```powershell
dotnet test .\costats.sln
dotnet build .\costats.sln -c Debug
```

Expected: tests and build pass.

- [x] **Step 6: Commit**

```powershell
git add src/costats.Infrastructure/Pricing src/costats.App/App.xaml.cs src/costats.Core/Pulse/RateCard.cs tests/costats.Tests/Pricing
git commit -m "feat: add embedded pricing catalog"
```

---

### Task 5: Add Privacy-Constrained GHCP Telemetry Digestor

**Files:**
- Create: `src/costats.Infrastructure/Expense/ConsumptionSliceAggregator.cs`
- Create: `src/costats.Infrastructure/Expense/CopilotTelemetryDigestor.cs`
- Create: `tests/costats.Tests/Expense/CopilotTelemetryDigestorTests.cs`

- [ ] **Step 1: Write GHCP parser tests**

Create `tests/costats.Tests/Expense/CopilotTelemetryDigestorTests.cs`:

```csharp
using costats.Infrastructure.Expense;
using costats.Infrastructure.Pricing;

namespace costats.Tests.Expense;

public sealed class CopilotTelemetryDigestorTests
{
    [Fact]
    public async Task DigestAsync_ExtractsOnlyModelTimestampAndTokens()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "events.jsonl");
        await File.WriteAllTextAsync(file, """
{"timestamp":"2026-06-29T01:02:03Z","model":"gpt-5","prompt":"secret prompt text","completion":"secret completion text","input_tokens":10,"cached_input_tokens":2,"output_tokens":5,"reasoning_tokens":1}
""");

        var slices = await CopilotTelemetryDigestor.DigestAsync(
            new EmbeddedPricingCatalog(),
            [root],
            DateOnly.FromDateTime(DateTime.Today.AddDays(-7)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            CancellationToken.None);

        Assert.Single(slices);
        Assert.Equal("gpt-5", slices[0].Model);
        Assert.Equal(10, slices[0].Tokens.StandardInput + slices[0].Tokens.CachedInput);
        Assert.Equal(5, slices[0].Tokens.GeneratedOutput + slices[0].Tokens.ReasoningOutput);
    }
}
```

- [ ] **Step 2: Add aggregation helper**

Create `src/costats.Infrastructure/Expense/ConsumptionSliceAggregator.cs` with `Add` and `Build` methods that aggregate by `DateOnly` and model into existing `ConsumptionSlice` instances.

- [ ] **Step 3: Implement digestor**

Create `src/costats.Infrastructure/Expense/CopilotTelemetryDigestor.cs` with:

```csharp
public static Task<IReadOnlyList<ConsumptionSlice>> DigestAsync(
    IPricingCatalog pricingCatalog,
    IEnumerable<string> roots,
    DateOnly since,
    DateOnly until,
    CancellationToken cancellationToken = default)
```

Implementation rules:
- Only enumerate files ending in `.jsonl`, `.json`, or `.log`.
- Skip files larger than 25 MB.
- Read line by line.
- Parse JSON using `JsonDocument`.
- Flatten OpenTelemetry `key`/`value` attributes and direct fields.
- Accepted token keys: `gen_ai.usage.input_tokens`, `llm.usage.prompt_tokens`, `input_tokens`, `prompt_tokens`, `gen_ai.usage.cached_input_tokens`, `cached_input_tokens`, `gen_ai.usage.output_tokens`, `completion_tokens`, `output_tokens`, `gen_ai.usage.reasoning_output_tokens`, `reasoning_tokens`.
- Accepted model keys: `gen_ai.response.model`, `gen_ai.request.model`, `model`, `model_name`, `ai.model`.
- Accepted timestamp keys: `time_unix_nano`, `timeUnixNano`, `timestamp`, `time`.
- Do not store or return any prompt/completion/message fields.

- [ ] **Step 4: Run GHCP tests**

Run:

```powershell
dotnet test .\tests\costats.Tests\costats.Tests.csproj --filter "FullyQualifiedName~CopilotTelemetryDigestor"
```

Expected: tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/costats.Infrastructure/Expense tests/costats.Tests/Expense
git commit -m "feat: digest GitHub Copilot telemetry locally"
```

---

### Task 6: Wire GHCP Telemetry Behind Opt-In Settings

**Files:**
- Modify: `src/costats.Application/Settings/AppSettings.cs`
- Modify: `src/costats.Infrastructure/Expense/ExpenseAnalyzer.cs`
- Modify: `src/costats.Infrastructure/Providers/CopilotPersonalSource.cs`
- Modify: `src/costats.App/App.xaml.cs`
- Create: `docs/COPILOT-TELEMETRY.md`

- [ ] **Step 1: Add settings**

Modify `AppSettings`:

```csharp
public bool CopilotTelemetryEnabled { get; set; } = false;
public string[] CopilotTelemetryRoots { get; set; } = [];
```

- [ ] **Step 2: Add analyzer entrypoint**

Add to `ExpenseAnalyzer`:

```csharp
public async Task<ConsumptionDigest?> AnalyzeCopilotTelemetryAsync(
    IPricingCatalog pricingCatalog,
    IEnumerable<string> roots,
    CancellationToken cancellationToken)
```

It should call `CopilotTelemetryDigestor.DigestAsync`, return `null` when there are no slices, and otherwise build the same `ConsumptionDigest` shape used by existing Codex/Claude analysis.

- [ ] **Step 3: Attach telemetry consumption to Copilot source**

Update `CopilotPersonalSource` constructor to accept `AppSettings`, `IPricingCatalog`, and `ExpenseAnalyzer`. In `ReadAsync`, when `settings.CopilotTelemetryEnabled` is `true`, call `AnalyzeCopilotTelemetryAsync`; otherwise leave `Consumption` null. Keep API quota behavior unchanged.

- [ ] **Step 4: Register dependencies**

In `App.xaml.cs`, register `ExpenseAnalyzer` and ensure `CopilotPersonalSource` is constructed by DI:

```csharp
services.AddSingleton<ExpenseAnalyzer>();
services.AddSingleton<ISignalSource, CopilotPersonalSource>();
```

- [ ] **Step 5: Document privacy behavior**

Create `docs/COPILOT-TELEMETRY.md` with:

```markdown
# GitHub Copilot Telemetry

GitHub Copilot telemetry support is disabled by default.

When enabled, costats reads local Copilot-related JSON, JSONL, and log files from configured roots and extracts only model IDs, timestamps, and token counters. It does not store prompt text, completion text, source files, chat messages, or raw telemetry lines.

Telemetry-derived usage stays on the local machine. costats does not send telemetry records to GitHub, OpenAI, OpenRouter, LiteLLM, or any other third party.

Leave `CopilotTelemetryEnabled` set to `false` to disable this feature.
```

- [ ] **Step 6: Run build and tests**

Run:

```powershell
dotnet test .\costats.sln
dotnet build .\costats.sln -c Debug
```

Expected: tests and build pass.

- [ ] **Step 7: Commit**

```powershell
git add src/costats.Application/Settings/AppSettings.cs src/costats.Infrastructure/Expense src/costats.Infrastructure/Providers/CopilotPersonalSource.cs src/costats.App/App.xaml.cs docs/COPILOT-TELEMETRY.md
git commit -m "feat: wire local Copilot telemetry behind opt-in settings"
```

---

### Task 7: Prepare Provider Display for More Providers

**Files:**
- Modify: `src/costats.App/ViewModels/PulseViewModel.cs`
- Test manually through build; add unit tests only if view-model construction can be tested without WPF dispatcher changes.

- [ ] **Step 1: Preserve all source profiles**

Modify `PulseViewModel.OnNext` so provider IDs that are not exactly `codex`, `claude`, or `copilot` remain in `Providers` and are not discarded from refresh/display metadata.

- [ ] **Step 2: Keep existing tabs stable**

Do not redesign XAML. Continue filling `Codex`, `Claude`, `Copilot`, and `ClaudeProfiles` for current UI compatibility. The generic `Providers` collection becomes the expansion surface for future Agy/GHCP/provider pages.

- [ ] **Step 3: Add refresh target fallback**

Modify `SelectedProviderId` so an unknown selected provider can be refreshed by ID once the UI exposes it. Current behavior for indices `0`, `1`, and Copilot remains unchanged.

- [ ] **Step 4: Run build**

Run:

```powershell
dotnet build .\costats.sln -c Debug
```

Expected: build passes.

- [ ] **Step 5: Commit**

```powershell
git add src/costats.App/ViewModels/PulseViewModel.cs
git commit -m "refactor: preserve dynamic provider readings"
```

---

### Task 8: Security and Privacy Verification

**Files:**
- Review: `src/costats.Infrastructure/Expense/CopilotTelemetryDigestor.cs`
- Review: `src/costats.Infrastructure/Providers/CopilotPersonalSource.cs`
- Review: `docs/COPILOT-TELEMETRY.md`

- [ ] **Step 1: Confirm opt-in default**

Run:

```powershell
rg -n "CopilotTelemetryEnabled|CopilotTelemetryRoots" src docs
```

Expected:
- `CopilotTelemetryEnabled` default is `false`.
- `CopilotTelemetryRoots` default is empty.
- docs state telemetry is disabled by default.

- [ ] **Step 2: Confirm no raw content retention**

Run:

```powershell
rg -n "prompt|completion|message|content|raw|WriteAllText|Log\\." src\costats.Infrastructure\Expense src\costats.Infrastructure\Providers docs\COPILOT-TELEMETRY.md
```

Expected:
- Digestor may mention prompt/completion fields only in comments or tests explaining ignored data.
- Production code does not log or persist raw telemetry line content.

- [ ] **Step 3: Confirm no network pricing refresh**

Run:

```powershell
rg -n "OpenRouter|LiteLLM|HttpClient|EnableNetworkRefresh|PricingRefresh" src tests docs
```

Expected:
- No background pricing refresh service is registered.
- `EnableNetworkRefresh` defaults to `false`.

- [ ] **Step 4: Full verification**

Run:

```powershell
dotnet test .\costats.sln
dotnet build .\costats.sln -c Release
git status --short
```

Expected:
- Tests pass.
- Release build passes.
- Only intentional files are modified.

- [ ] **Step 5: Commit verification docs if changed**

```powershell
git add docs/COPILOT-TELEMETRY.md
git commit -m "docs: document Copilot telemetry privacy model"
```

Skip this commit if the docs were already committed in Task 6 and no doc changes are pending.

---

## Self-Review

- Spec coverage: Riley pricing/model matching is covered by Tasks 2-4. GHCP local usage is covered by Tasks 5-6. Provider expansion groundwork is covered by Task 7. Privacy/security implications are covered before implementation and verified in Task 8.
- Placeholder scan: the plan intentionally avoids open-ended implementation slots; each task has concrete files, commands, expected results, and acceptance checks.
- Type consistency: pricing types are introduced in Task 3 before being consumed by Tasks 4-6; `IPricingCatalog` is registered before Copilot telemetry uses it.

## Execution Recommendation

Use subagent-driven execution if available because Tasks 3, 5, and 6 can be reviewed independently. Use one commit per task so any risky GHCP telemetry changes can be reverted without losing pricing groundwork.
