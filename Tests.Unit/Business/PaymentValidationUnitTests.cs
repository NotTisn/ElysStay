using Application.Features.Payments.Commands;
using Application.Features.Payments.Validators;
using FluentAssertions;
using Xunit;

namespace ElysStay.Tests.Unit.Business;

public class PaymentValidationUnitTests
{
    [Fact]
    public void BatchRecordPaymentsValidator_RejectsZeroAmount()
    {
        var validator = new BatchRecordPaymentsCommandValidator();

        var result = validator.Validate(new BatchRecordPaymentsCommand
        {
            Payments = new[]
            {
                new BatchPaymentEntry
                {
                    InvoiceId = Guid.NewGuid(),
                    Amount = 0,
                    PaymentMethod = "Cash"
                }
            }
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Số tiền phải lớn hơn 0"));
    }

    [Fact]
    public void BatchRecordPaymentsValidator_RejectsNegativeAmount()
    {
        var validator = new BatchRecordPaymentsCommandValidator();

        var result = validator.Validate(new BatchRecordPaymentsCommand
        {
            Payments = new[]
            {
                new BatchPaymentEntry
                {
                    InvoiceId = Guid.NewGuid(),
                    Amount = -1000,
                    PaymentMethod = "BankTransfer"
                }
            }
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Số tiền phải lớn hơn 0"));
    }

    [Fact]
    public void ProcessPaymentWebhookValidator_RejectsZeroAmount()
    {
        var validator = new ProcessPaymentWebhookCommandValidator();

        var result = validator.Validate(new ProcessPaymentWebhookCommand
        {
            InvoiceId = Guid.NewGuid(),
            Amount = 0,
            ReferenceCode = "BANK-REF-003"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Số tiền phải lớn hơn 0"));
    }
    }
