namespace Rappix.Orders.Application.Sagas.Messages;

// Decisiones que llegan desde los endpoints REST (merchant/cliente) y que la saga consume. Se publican
// por el outbox al ejecutar el comando correspondiente y se correlacionan por OrderId.

/// <summary>El merchant acepto el pedido.</summary>
public sealed record MerchantAccepted(Guid OrderId);

/// <summary>El merchant rechazo el pedido.</summary>
public sealed record MerchantRejected(Guid OrderId, string Reason);

/// <summary>El cliente (o un proceso) solicito cancelar el pedido.</summary>
public sealed record OrderCancellationRequested(Guid OrderId, string Reason);
