using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Quotes;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Infrastructure.Persistence.Converters;

/// <summary>Convierte el identificador fuertemente tipado QuoteId a Guid para la persistencia.</summary>
internal sealed class QuoteIdConverter()
    : ValueConverter<QuoteId, Guid>(id => id.Value, value => new QuoteId(value));

/// <summary>Convierte el identificador fuertemente tipado CouponId a Guid para la persistencia.</summary>
internal sealed class CouponIdConverter()
    : ValueConverter<CouponId, Guid>(id => id.Value, value => new CouponId(value));

/// <summary>Convierte el identificador fuertemente tipado SurgeRuleId a Guid para la persistencia.</summary>
internal sealed class SurgeRuleIdConverter()
    : ValueConverter<SurgeRuleId, Guid>(id => id.Value, value => new SurgeRuleId(value));
