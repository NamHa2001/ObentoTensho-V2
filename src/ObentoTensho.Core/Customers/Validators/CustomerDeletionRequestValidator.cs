using FluentValidation;
using ObentoTensho.Contracts.Dtos.Customers;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Customers.Validators;

public sealed class CustomerDeletionRequestValidator : AbstractValidator<CustomerDeletionRequest>
{
    public CustomerDeletionRequestValidator() => RuleFor(r => r.Reason).RequiredText(300);
}
