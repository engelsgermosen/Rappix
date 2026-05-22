using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Items.GetPhotoUrl;

/// <summary>Genera la URL firmada (1h) de la foto de un item.</summary>
public sealed record GetItemPhotoUrlQuery(Guid ItemId) : IRequest<Result<string>>;
