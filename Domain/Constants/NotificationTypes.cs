namespace Domain.Constants;

/// <summary>
/// Canonical notification type strings used when creating Notification entities.
/// Keep in sync with the FE NotificationType union in types/api.ts.
/// </summary>
public static class NotificationTypes
{
    public const string ContractCreated = "CONTRACT_CREATED";
    public const string ContractTerminated = "CONTRACT_TERMINATED";
    public const string ContractRenewed = "CONTRACT_RENEWED";
    public const string ContractExpiryAlert = "CONTRACT_EXPIRY_ALERT";
    public const string InvoiceSent = "INVOICE_SENT";
    public const string InvoiceVoided = "INVOICE_VOIDED";
    public const string InvoiceOverdue = "INVOICE_OVERDUE";
    public const string PaymentRecorded = "PAYMENT_RECORDED";
    public const string ReservationExpired = "RESERVATION_EXPIRED";
    public const string Issue = "ISSUE";
}
