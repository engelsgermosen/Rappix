using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Tests.Unit;

/// <summary>Pruebas unitarias del agregado MerchantCatalog (gating y categorias).</summary>
public sealed class MerchantCatalogTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_StartsEnabled()
    {
        MerchantCatalog catalog = NewCatalog();

        catalog.IsEnabled.Should().BeTrue();
        catalog.VerticalType.Should().Be(VerticalType.Food);
        catalog.Categories.Should().BeEmpty();
    }

    [Fact]
    public void Disable_ThenEnable_TogglesGating()
    {
        MerchantCatalog catalog = NewCatalog();

        catalog.Disable(Now);
        catalog.IsEnabled.Should().BeFalse();

        catalog.Enable(Now);
        catalog.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void AddCategory_Succeeds_AndIsDiscoverable()
    {
        MerchantCatalog catalog = NewCatalog();

        Result<Category> result = catalog.AddCategory("Bebidas", 1, Now);

        result.IsSuccess.Should().BeTrue();
        catalog.HasCategory(result.Value.Id).Should().BeTrue();
        catalog.Categories.Should().ContainSingle();
    }

    [Fact]
    public void AddCategory_WithDuplicateName_Fails()
    {
        MerchantCatalog catalog = NewCatalog();
        catalog.AddCategory("Bebidas", 1, Now);

        Result<Category> result = catalog.AddCategory("bebidas", 2, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        catalog.Categories.Should().ContainSingle();
    }

    [Fact]
    public void AddCategory_WithEmptyName_Fails()
    {
        MerchantCatalog catalog = NewCatalog();

        Result<Category> result = catalog.AddCategory("  ", 1, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void RemoveCategory_WhenMissing_Fails()
    {
        MerchantCatalog catalog = NewCatalog();

        Result result = catalog.RemoveCategory(Guid.NewGuid(), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    private static MerchantCatalog NewCatalog() =>
        MerchantCatalog.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), VerticalType.Food, Now);
}
