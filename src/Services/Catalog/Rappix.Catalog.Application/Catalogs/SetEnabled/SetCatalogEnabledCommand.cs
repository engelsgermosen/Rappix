using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Catalogs.SetEnabled;

/// <summary>Habilita o deshabilita el catalogo de un merchant (consumers de MerchantActivated/MerchantSuspended).</summary>
public sealed record SetCatalogEnabledCommand(Guid MerchantId, bool Enabled) : IRequest<Result>;
