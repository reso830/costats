namespace costats.Application.Pricing;

public interface IPricingCatalog
{
    Task<ModelPricing?> LookupAsync(string modelId, string? providerHint, CancellationToken cancellationToken);
}
