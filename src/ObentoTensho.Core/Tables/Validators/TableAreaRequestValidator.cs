using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Core.Tables.Validators;

public sealed class TableAreaRequestValidator : AbstractValidator<TableAreaRequest>
{
    public const int NameMaxLength = 100;

    public TableAreaRequestValidator()
    {
        RuleFor(r => r.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithErrorCode(FieldErrorCodes.Required)
            .Must(n => n is null || n.Trim().Length <= NameMaxLength).WithErrorCode(FieldErrorCodes.TooLong);
    }
}
