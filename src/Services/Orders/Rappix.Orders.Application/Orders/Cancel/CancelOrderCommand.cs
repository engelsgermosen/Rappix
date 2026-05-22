using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Orders.Application.Orders.Cancel;

/// <summary>El cliente cancela el pedido (solo en estados cancelables). Publica OrderCancellationRequested.</summary>
public sealed record CancelOrderCommand(Guid OrderId, Guid CustomerUserId, string? Reason) : IRequest<Result>;
