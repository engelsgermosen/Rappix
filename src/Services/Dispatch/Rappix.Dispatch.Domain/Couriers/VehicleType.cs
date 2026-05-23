namespace Rappix.Dispatch.Domain.Couriers;

/// <summary>Tipo de vehiculo del courier (vocabulario RD para Fase 6).</summary>
public enum VehicleType
{
    /// <summary>Motocicleta.</summary>
    Moto = 0,

    /// <summary>Bicicleta.</summary>
    Bici = 1,

    /// <summary>Carro / vehiculo a motor mayor que moto.</summary>
    Carro = 2,
}
