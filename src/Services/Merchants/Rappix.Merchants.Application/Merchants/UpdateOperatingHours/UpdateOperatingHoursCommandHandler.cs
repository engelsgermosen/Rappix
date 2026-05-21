using System.Globalization;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.UpdateOperatingHours;

/// <summary>Parsea los rangos validados y reemplaza los horarios del merchant.</summary>
internal sealed class UpdateOperatingHoursCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<UpdateOperatingHoursCommand, Result<MerchantResponse>>
{
    public async Task<Result<MerchantResponse>> Handle(UpdateOperatingHoursCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByOwnerAsync(command.OwnerUserId, cancellationToken);
        if (merchant is null)
        {
            return Result.Failure<MerchantResponse>(MerchantErrors.NotFound);
        }

        OperatingHoursRange[] ranges =
        [
            .. command.Hours.Select(input => new OperatingHoursRange(
                Enum.Parse<DayOfWeek>(input.DayOfWeek, ignoreCase: true),
                TimeOnly.Parse(input.OpensAt, CultureInfo.InvariantCulture),
                TimeOnly.Parse(input.ClosesAt, CultureInfo.InvariantCulture)))
        ];

        Result replaced = merchant.ReplaceOperatingHours(ranges, clock.UtcNow);
        if (replaced.IsFailure)
        {
            return Result.Failure<MerchantResponse>(replaced.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MerchantResponse.From(merchant);
    }
}
