using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;
using NpgsqlTypes;
using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core (+ PostGIS) del repositorio de merchants.</summary>
internal sealed class MerchantRepository(MerchantsDbContext context) : IMerchantRepository
{
    public void Add(Merchant merchant) => context.Merchants.Add(merchant);

    public Task<Merchant?> GetByIdAsync(MerchantId id, CancellationToken cancellationToken) =>
        WithChildren().FirstOrDefaultAsync(merchant => merchant.Id == id, cancellationToken);

    public Task<Merchant?> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        WithChildren().FirstOrDefaultAsync(merchant => merchant.OwnerUserId == ownerUserId, cancellationToken);

    public Task<Merchant?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        Slug value = Slug.FromTrusted(slug);
        return WithChildren().FirstOrDefaultAsync(merchant => merchant.Slug == value, cancellationToken);
    }

    public Task<bool> ExistsByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        context.Merchants.AnyAsync(merchant => merchant.OwnerUserId == ownerUserId, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, MerchantId? excluding, CancellationToken cancellationToken)
    {
        Slug value = Slug.FromTrusted(slug);
        if (excluding is null)
        {
            return context.Merchants.AnyAsync(merchant => merchant.Slug == value, cancellationToken);
        }

        MerchantId exclude = excluding.Value;
        return context.Merchants.AnyAsync(merchant => merchant.Slug == value && merchant.Id != exclude, cancellationToken);
    }

    public Task<bool> RncExistsAsync(string rnc, MerchantId? excluding, CancellationToken cancellationToken)
    {
        Rnc value = Rnc.FromTrusted(rnc);
        if (excluding is null)
        {
            return context.Merchants.AnyAsync(merchant => merchant.Rnc == value, cancellationToken);
        }

        MerchantId exclude = excluding.Value;
        return context.Merchants.AnyAsync(merchant => merchant.Rnc == value && merchant.Id != exclude, cancellationToken);
    }

    public async Task<PagedResult<Merchant>> ListByStatusAsync(MerchantStatus status, PagedRequest paging, CancellationToken cancellationToken)
    {
        IQueryable<Merchant> query = context.Merchants.Where(merchant => merchant.Status == status);

        int total = await query.CountAsync(cancellationToken);
        List<Merchant> items = await WithChildren(query)
            .OrderByDescending(merchant => merchant.CreatedAtUtc)
            .Skip(paging.Skip)
            .Take(paging.Take)
            .ToListAsync(cancellationToken);

        return new PagedResult<Merchant>(items, paging.Page, paging.Take, total);
    }

    public async Task<IReadOnlyList<Merchant>> SearchNearbyAsync(Point point, VerticalType? vertical, PagedRequest paging, CancellationToken cancellationToken)
    {
        // El UNION poligono (geometry, ST_Contains) + circulo (geography, ST_DWithin con radio por fila)
        // no se traduce de forma fiable en LINQ (mezcla de tipos y cast a geography), asi que se usa
        // SQL PostGIS explicito (guideline: FromSql documentado). El point va como geometry(4326) y se
        // castea a geography para las distancias en metros. Ordena por la zona mas cercana.
        var pointParam = new NpgsqlParameter("point", point);
        var verticalParam = new NpgsqlParameter("vertical", NpgsqlDbType.Text)
        {
            Value = (object?)vertical?.ToString() ?? DBNull.Value,
        };
        var skipParam = new NpgsqlParameter("skip", paging.Skip);
        var takeParam = new NpgsqlParameter("take", paging.Take);

        const string sql =
            """
            SELECT m.*
            FROM "merchants"."merchants" AS m
            WHERE m."Status" = 'Active'
              AND m."IsDeleted" = FALSE
              AND (@vertical IS NULL OR m."VerticalType" = @vertical)
              AND EXISTS (
                  SELECT 1 FROM "merchants"."service_areas" AS sa
                  WHERE sa."MerchantId" = m."Id"
                    AND ((sa."Type" = 'Polygon' AND ST_Contains(sa."Polygon", @point))
                      OR (sa."Type" = 'Circle' AND ST_DWithin(sa."Center", @point::geography, sa."RadiusMeters")))
              )
            ORDER BY (
                SELECT MIN(CASE WHEN sa."Type" = 'Polygon'
                                THEN ST_Distance(sa."Polygon"::geography, @point::geography)
                                ELSE ST_Distance(sa."Center", @point::geography) END)
                FROM "merchants"."service_areas" AS sa
                WHERE sa."MerchantId" = m."Id") ASC
            OFFSET @skip LIMIT @take
            """;

        return await context.Merchants
            .FromSqlRaw(sql, pointParam, verticalParam, skipParam, takeParam)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Merchant> WithChildren() => WithChildren(context.Merchants);

    private static IQueryable<Merchant> WithChildren(IQueryable<Merchant> query) =>
        query
            .Include(merchant => merchant.ServiceAreas)
            .Include(merchant => merchant.OperatingHours);
}
