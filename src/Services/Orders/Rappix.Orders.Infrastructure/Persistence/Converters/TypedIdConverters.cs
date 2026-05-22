using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Infrastructure.Persistence.Converters;

/// <summary>Convierte el identificador fuertemente tipado OrderId a Guid para la persistencia.</summary>
internal sealed class OrderIdConverter()
    : ValueConverter<OrderId, Guid>(id => id.Value, value => new OrderId(value));
