using FluentValidation;
using ObentoTensho.Contracts.Dtos.Invoices;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Invoices.Domain;

namespace ObentoTensho.Core.Invoices.Validators;

public sealed class InvoiceBuyerRequestValidator : AbstractValidator<InvoiceBuyerRequest>
{
    public InvoiceBuyerRequestValidator()
    {
        RuleFor(r => r.BuyerType).IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.BuyerName)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => v is null || v.Trim().Length <= InvoiceRules.NameMaxLength).WithErrorCode(FieldErrorCodes.TooLong)
            .NoMarkup();
        RuleFor(r => r.NationalId)
            .Must(v => InvoiceRules.IsNationalId(v?.Trim())).WithErrorCode(FieldErrorCodes.InvalidFormat)
            .When(r => r.BuyerType == InvoiceBuyerType.Individual);
        RuleFor(r => r.TaxCode)
            .Must(v => InvoiceRules.IsTaxCode(v?.Trim())).WithErrorCode(FieldErrorCodes.InvalidFormat)
            .When(r => r.BuyerType == InvoiceBuyerType.Company);
        RuleFor(r => r.Address)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithErrorCode(FieldErrorCodes.Required)
            .When(r => r.BuyerType == InvoiceBuyerType.Company);
        RuleFor(r => r.Address!)
            .Must(v => v is null || v.Trim().Length <= InvoiceRules.AddressMaxLength).WithErrorCode(FieldErrorCodes.TooLong)
            .NoMarkup();

        // Chủ quán 2026-11-13: mọi ô trên form hóa đơn đều bắt buộc — email nhận hóa đơn (cả hai loại), SĐT người mua cá nhân
        RuleFor(r => r.Email!).ValidEmail();
        RuleFor(r => r.Phone)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithErrorCode(FieldErrorCodes.Required)
            .Must(v => string.IsNullOrWhiteSpace(v) || VietnamesePhoneNumber.TryNormalize(v, out _)).WithErrorCode(FieldErrorCodes.InvalidFormat)
            .When(r => r.BuyerType == InvoiceBuyerType.Individual);
        RuleFor(r => r.Phone)
            .Must(v => VietnamesePhoneNumber.TryNormalize(v, out _)).WithErrorCode(FieldErrorCodes.InvalidFormat)
            .When(r => r.BuyerType == InvoiceBuyerType.Company && !string.IsNullOrWhiteSpace(r.Phone));
        RuleFor(r => r.Note!)
            .Must(v => v is null || v.Trim().Length <= InvoiceRules.NoteMaxLength).WithErrorCode(FieldErrorCodes.TooLong)
            .NoMarkup();
    }
}
