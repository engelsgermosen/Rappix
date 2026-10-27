using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Rappix.Orders.Application.Observability;

/// <summary>
/// ActivitySource y Meter propios de Orders. El nombre "orders" coincide con el serviceName pasado a
/// AddRappixObservability, que ya hace AddSource("orders")/AddMeter("orders").
/// </summary>
public static class OrdersTelemetry
{
    public static readonly ActivitySource ActivitySource = new("orders");

    private static readonly Meter Meter = new("orders");

    public static readonly Counter<long> OrdersPlaced = Meter.CreateCounter<long>("orders.placed");

    public static readonly Counter<long> OrdersCompleted = Meter.CreateCounter<long>("orders.completed");
}
