using FluentValidation;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Admin.ListByStatus;

/// <summary>Validacion del listado por estado.</summary>
internal sealed class ListMerchantsByStatusQueryValidator : AbstractValidator<ListMerchantsByStatusQuery>
{
    public ListMerchantsByStatusQueryValidator() =>
        RuleFor(query => query.Status)
            .Must(value => Enum.TryParse<MerchantStatus>(value, ignoreCase: true, out _))
            .WithMessage("status invalido (Draft, Pending, Active, Paused, Suspended o Rejected).");
}
