using FluentValidation;
using ObentoTensho.Contracts.Dtos.Customers;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Customers.Validators;

public sealed class CustomerStatusRequestValidator : AbstractValidator<CustomerStatusRequest>
{
    public CustomerStatusRequestValidator() => RuleFor(r => r.Reason).RequiredText(300);
}
