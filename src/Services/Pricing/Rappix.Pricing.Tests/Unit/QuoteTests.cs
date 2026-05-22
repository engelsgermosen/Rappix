using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Quotes;
using Rappix.Pricing.Domain.Quotes.Events;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Pruebas del agregado Quote (creacion, expiracion perezosa, transiciones de estado).</summary>
public sealed class QuoteTests
{
    private static readonly DateTime Now = new(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_NoLines_Fails()
    {
        Result<Quote> result = Quote.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), VerticalType.Food, "DOP", [], Breakdown(), null, null, Now, 10);

        result.Error.Should().Be(QuoteErrors.NoLines);
    }

    [Fact]
    public void Create_InvalidExpiry_Fails()
    {
        Quote.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), VerticalType.Food, "DOP", [Line()], Breakdown(), null, null, Now, 0)
            .Error.Should().Be(QuoteErrors.InvalidExpiration);
    }

    [Fact]
    public void Create_Succeeds_RaisesQuoteCreatedDomainEvent()
    {
        Quote quote = NewQuote();

        quote.Status.Should().Be(QuoteStatus.Active);
        quote.ExpiresAtUtc.Should().Be(Now.AddMinutes(10));
        quote.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is QuoteCreatedDomainEvent);
    }

    [Fact]
    public void IsExpiredAt_BeforeExpiry_False()
    {
        NewQuote().IsExpiredAt(Now.AddMinutes(5)).Should().BeFalse();
    }

    [Fact]
    public void IsExpiredAt_AfterExpiry_True()
    {
        NewQuote().IsExpiredAt(Now.AddMinutes(11)).Should().BeTrue();
    }

    [Fact]
    public void Consume_Active_Succeeds()
    {
        Quote quote = NewQuote();

        Result result = quote.Consume(Now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        quote.Status.Should().Be(QuoteStatus.Consumed);
        quote.ConsumedAtUtc.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void Consume_AlreadyConsumed_Fails()
    {
        Quote quote = NewQuote();
        quote.Consume(Now.AddMinutes(1));

        quote.Consume(Now.AddMinutes(2)).Error.Should().Be(QuoteErrors.AlreadyConsumed);
    }

    [Fact]
    public void Consume_AfterExpiry_FailsAndMarksExpired()
    {
        Quote quote = NewQuote();

        Result result = quote.Consume(Now.AddMinutes(11));

        result.Error.Should().Be(QuoteErrors.Expired);
        quote.Status.Should().Be(QuoteStatus.Expired);
    }

    [Fact]
    public void MarkExpired_OnlyTransitionsFromActive()
    {
        Quote quote = NewQuote();
        quote.Consume(Now.AddMinutes(1));

        quote.MarkExpired();

        quote.Status.Should().Be(QuoteStatus.Consumed);
    }

    private static QuoteLine Line() =>
        QuoteLine.Create(Guid.CreateVersion7(), "Item", 100m, 0m, 1).Value;

    private static PriceBreakdown Breakdown(decimal total = 100m) =>
        new() { Subtotal = total, SurgeMultiplier = 1m, Total = total };

    private static Quote NewQuote(int expiryMinutes = 10) =>
        Quote.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), VerticalType.Food, "DOP", [Line()], Breakdown(), null, null, Now, expiryMinutes).Value;
}
