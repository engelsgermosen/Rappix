using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Infrastructure.Persistence.Converters;

/// <summary>Convierte el identificador fuertemente tipado CourierId a Guid para la persistencia.</summary>
internal sealed class CourierIdConverter()
    : ValueConverter<CourierId, Guid>(id => id.Value, value => new CourierId(value));
