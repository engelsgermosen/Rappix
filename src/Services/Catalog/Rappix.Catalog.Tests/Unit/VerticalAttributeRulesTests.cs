using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Items.Attributes;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Tests.Unit;

/// <summary>Pruebas unitarias de la validacion de atributos por vertical.</summary>
public sealed class VerticalAttributeRulesTests
{
    [Fact]
    public void Food_WithKnownAttributes_Succeeds()
    {
        var attributes = new Dictionary<string, string> { ["calories"] = "500", ["isVegetarian"] = "true" };

        Result result = VerticalAttributeRules.Validate(VerticalType.Food, attributes);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Food_WithEmptyAttributes_Succeeds()
    {
        Result result = VerticalAttributeRules.Validate(VerticalType.Food, new Dictionary<string, string>());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void UnknownKey_Fails()
    {
        var attributes = new Dictionary<string, string> { ["color"] = "rojo" };

        Result result = VerticalAttributeRules.Validate(VerticalType.Food, attributes);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.Attributes.UnknownKey");
    }

    [Fact]
    public void Pharmacy_WithoutRequiredPrescriptionFlag_Fails()
    {
        Result result = VerticalAttributeRules.Validate(VerticalType.Pharmacy, new Dictionary<string, string>());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.Attributes.MissingRequired");
    }

    [Fact]
    public void Pharmacy_WithPrescriptionFlag_Succeeds()
    {
        var attributes = new Dictionary<string, string> { ["requiresPrescription"] = "false" };

        Result result = VerticalAttributeRules.Validate(VerticalType.Pharmacy, attributes);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Pharmacy_WithInvalidBooleanFlag_Fails()
    {
        var attributes = new Dictionary<string, string> { ["requiresPrescription"] = "quizas" };

        Result result = VerticalAttributeRules.Validate(VerticalType.Pharmacy, attributes);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.Attributes.InvalidBoolean");
    }

    [Fact]
    public void Food_WithNonNumericCalories_Fails()
    {
        var attributes = new Dictionary<string, string> { ["calories"] = "muchas" };

        Result result = VerticalAttributeRules.Validate(VerticalType.Food, attributes);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.Attributes.InvalidNumber");
    }
}
