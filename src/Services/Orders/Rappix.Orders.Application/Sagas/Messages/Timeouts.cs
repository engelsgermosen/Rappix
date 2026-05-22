namespace Rappix.Orders.Application.Sagas.Messages;

// Mensajes de timeout programados con el scheduler (Quartz). La saga los agenda al entrar a un estado de
// espera y los cancela (Unschedule) cuando llega el evento esperado; si disparan, compensan. El nombre
// lleva "Expired" para no colisionar con las propiedades Schedule de la maquina de estados.

/// <summary>Expiro la espera de aceptacion del merchant.</summary>
public sealed record MerchantTimeoutExpired(Guid OrderId);

/// <summary>Expiro la espera de confirmacion del pago.</summary>
public sealed record PaymentTimeoutExpired(Guid OrderId);

/// <summary>Expiro la espera de asignacion de courier.</summary>
public sealed record CourierTimeoutExpired(Guid OrderId);
