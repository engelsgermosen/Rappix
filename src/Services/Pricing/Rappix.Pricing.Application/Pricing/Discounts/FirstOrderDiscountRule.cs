using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Configuration;

namespace Rappix.Pricing.Application.Pricing.Discounts;

/// <summary>
/// Descuento de primera compra: aplica cuando el caller indica que es el primer pedido del cliente. En
/// Fase 4 el flag IsFirstOrder llega como input (lo proveera Orders); el tipo y valor son configurables.
/// </summary>
internal sealed class FirstOrderDiscountRule(IOptions<PricingOptions> options) : IDiscountRule
{
    public Task<Result<DiscountEvaluation?>> EvaluateAsync(DiscountRuleContext context, CancellationToken cancellationToken)
    {
        FirstOrderDiscountOptions config = options.Value.FirstOrderDiscount;

        if (!config.Enabled || !context.IsFirstOrder)
        {
            return Task.FromResult(Result.Success<DiscountEvaluation?>(null));
        }

        var evaluation = new DiscountEvaluation("FirstOrder", config.Type, config.Value);
        return Task.FromResult(Result.Success<DiscountEvaluation?>(evaluation));
    }
}
