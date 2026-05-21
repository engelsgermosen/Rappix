using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Infrastructure.Persistence.Converters;

/// <summary>Convierte el identificador fuertemente tipado MerchantId a Guid para la persistencia.</summary>
internal sealed class MerchantIdConverter()
    : ValueConverter<MerchantId, Guid>(id => id.Value, value => new MerchantId(value));
