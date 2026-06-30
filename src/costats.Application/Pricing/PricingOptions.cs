namespace costats.Application.Pricing;

public sealed class PricingOptions
{
    public bool EnableNetworkRefresh { get; set; } = false;
    public int RefreshIntervalHours { get; set; } = 24;
}
