using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Items.Delete;

/// <summary>Borrado logico de un item del merchant.</summary>
public sealed record DeleteItemCommand(Guid MerchantId, Guid ItemId) : IRequest<Result>;
