using FluentValidation;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Application.Catalogs.AddCategory;

/// <summary>Validacion del comando de alta de categoria.</summary>
internal sealed class AddCategoryCommandValidator : AbstractValidator<AddCategoryCommand>
{
    public AddCategoryCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(MerchantCatalog.MaxCategoryNameLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}
