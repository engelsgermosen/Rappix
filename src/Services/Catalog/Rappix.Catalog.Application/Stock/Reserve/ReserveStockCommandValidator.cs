using FluentValidation;

namespace Rappix.Catalog.Application.Stock.Reserve;

/// <summary>Valida la reserva: pedido e items presentes, cantidades positivas, items distintos y TTL positivo.</summary>
internal sealed class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.TtlSeconds).GreaterThan(0);
        RuleFor(command => command.Lines).NotEmpty();
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(item => item.ItemId).NotEmpty();
            line.RuleFor(item => item.Quantity).GreaterThan(0);
        });
        RuleFor(command => command.Lines)
            .Must(lines => lines.Select(line => line.ItemId).Distinct().Count() == lines.Count)
            .WithMessage("Las lineas de reserva no pueden repetir el mismo item.");
    }
}
