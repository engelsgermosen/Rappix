using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Domain.Items;
using Rappix.Catalog.Domain.Items.Events;

namespace Rappix.Catalog.Tests.Unit;

/// <summary>Pruebas unitarias del agregado Item y el value object Money.</summary>
public sealed class ItemAggregateTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid MerchantId = Guid.CreateVersion7();

    [Fact]
    public void Create_Succeeds_AndRaisesItemCreated()
    {
        Result<Item> result = Item.Create(MerchantId, null, "Pizza Margarita", "Clasica", Dop(250m), tracksInventory: false, attributes: null, Now);

        result.IsSuccess.Should().BeTrue();
        Item item = result.Value;
        item.Name.Should().Be("Pizza Margarita");
        item.IsAvailable.Should().BeTrue();
        item.Price.Amount.Should().Be(250m);
        item.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is ItemCreatedDomainEvent);
    }

    [Fact]
    public void Create_WithEmptyName_Fails()
    {
        Result<Item> result = Item.Create(MerchantId, null, "   ", null, Dop(10m), false, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_StoresAttributes()
    {
        var attributes = new Dictionary<string, string> { ["spicyLevel"] = "2" };

        Item item = Item.Create(MerchantId, null, "Tacos", null, Dop(180m), false, attributes, Now).Value;

        item.Attributes.Should().ContainKey("spicyLevel");
    }

    [Fact]
    public void UpdateDetails_WhenDeleted_Fails()
    {
        Item item = NewItem();
        item.Delete(Now);

        Result result = item.UpdateDetails(null, "Nuevo", null, Dop(99m), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Delete_HidesItem()
    {
        Item item = NewItem();

        item.Delete(Now);

        item.IsDeleted.Should().BeTrue();
        item.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void AddModifier_WithInvalidRange_Fails()
    {
        Item item = NewItem();

        Result<Modifier> result = item.AddModifier("Tamano", isRequired: true, minSelections: 2, maxSelections: 1, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void AddModifierOption_ToExistingGroup_Succeeds()
    {
        Item item = NewItem();
        Modifier modifier = item.AddModifier("Tamano", isRequired: true, minSelections: 1, maxSelections: 1, Now).Value;

        Result result = item.AddModifierOption(modifier.Id, "Grande", 50m, Now);

        result.IsSuccess.Should().BeTrue();
        item.Modifiers.Single().Options.Should().ContainSingle();
    }

    [Fact]
    public void AddModifierOption_ToMissingGroup_Fails()
    {
        Item item = NewItem();

        Result result = item.AddModifierOption(Guid.NewGuid(), "Grande", 50m, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Money_WithNegativeAmount_Fails()
    {
        Result<Money> result = Money.Create(-1m);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Money_DefaultsToDop_AndRoundsToTwoDecimals()
    {
        Money money = Money.Create(10.999m).Value;

        money.Currency.Should().Be("DOP");
        money.Amount.Should().Be(11.00m);
    }

    [Fact]
    public void Money_WithInvalidCurrency_Fails()
    {
        Result<Money> result = Money.Create(10m, "DOLLARS");

        result.IsFailure.Should().BeTrue();
    }

    private static Item NewItem() =>
        Item.Create(MerchantId, null, "Item", null, Dop(100m), false, null, Now).Value;

    private static Money Dop(decimal amount) => Money.Create(amount).Value;
}
