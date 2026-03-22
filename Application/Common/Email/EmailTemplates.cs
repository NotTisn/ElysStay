namespace Application.Common.Email;

/// <summary>
/// Vietnamese email templates for all transactional notifications.
/// Each method returns (Subject, HtmlBody) tuple.
/// </summary>
public static class EmailTemplates
{
    private static string Wrap(string content) => $$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <style>
                body { font-family: 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background: #f5f5f5; margin: 0; padding: 20px; color: #333; }
                .container { max-width: 600px; margin: 0 auto; background: #fff; border-radius: 8px; padding: 32px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
                .header { text-align: center; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 2px solid #3b82f6; }
                .header h1 { color: #1e3a5f; font-size: 22px; margin: 0; letter-spacing: 1px; }
                .content { line-height: 1.7; font-size: 15px; }
                .info { background: #f0f9ff; border-left: 4px solid #3b82f6; padding: 12px 16px; margin: 16px 0; border-radius: 0 4px 4px 0; }
                .warn { background: #fef3c7; border-left: 4px solid #f59e0b; padding: 12px 16px; margin: 16px 0; border-radius: 0 4px 4px 0; }
                .success { background: #ecfdf5; border-left: 4px solid #059669; padding: 12px 16px; margin: 16px 0; border-radius: 0 4px 4px 0; }
                .amount { font-size: 22px; font-weight: 700; color: #059669; }
                .footer { margin-top: 32px; padding-top: 16px; border-top: 1px solid #eee; color: #999; font-size: 12px; text-align: center; }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="header"><h1>ElysStay</h1></div>
                <div class="content">{{content}}</div>
                <div class="footer">
                    <p>Email này được gửi tự động từ hệ thống quản lý nhà trọ ElysStay.</p>
                    <p>Vui lòng không trả lời email này.</p>
                </div>
            </div>
        </body>
        </html>
        """;

    // ── Invoice ──

    public static (string Subject, string Html) InvoiceSent(
        string tenantName, string roomNumber, string buildingName,
        int month, int year, decimal totalAmount, DateOnly dueDate) =>
    (
        $"Hóa đơn tháng {month}/{year} — {buildingName}",
        Wrap($"""
            <p>Xin chào <strong>{tenantName}</strong>,</p>
            <p>Hóa đơn tháng <strong>{month}/{year}</strong> của bạn đã được gửi.</p>
            <div class="info">
                <p><strong>Phòng:</strong> {roomNumber} — {buildingName}</p>
                <p><strong>Tổng tiền:</strong> <span class="amount">{totalAmount:N0}₫</span></p>
                <p><strong>Hạn thanh toán:</strong> {dueDate:dd/MM/yyyy}</p>
            </div>
            <p>Vui lòng thanh toán trước hạn để tránh phát sinh thêm chi phí.</p>
            """)
    );

    public static (string Subject, string Html) InvoiceOverdue(
        string tenantName, string roomNumber, string buildingName,
        int month, int year, decimal totalAmount) =>
    (
        $"⚠ Hóa đơn quá hạn — tháng {month}/{year}",
        Wrap($"""
            <p>Xin chào <strong>{tenantName}</strong>,</p>
            <div class="warn">
                <p><strong>Hóa đơn tháng {month}/{year}</strong> của phòng <strong>{roomNumber}</strong> tại <strong>{buildingName}</strong> đã <strong>quá hạn thanh toán</strong>.</p>
                <p><strong>Số tiền:</strong> <span class="amount">{totalAmount:N0}₫</span></p>
            </div>
            <p>Vui lòng thanh toán ngay để tránh ảnh hưởng đến hợp đồng thuê.</p>
            """)
    );

    public static (string Subject, string Html) InvoiceVoided(
        string tenantName, int month, int year, string buildingName) =>
    (
        $"Hóa đơn đã hủy — tháng {month}/{year}",
        Wrap($"""
            <p>Xin chào <strong>{tenantName}</strong>,</p>
            <p>Hóa đơn tháng <strong>{month}/{year}</strong> tại <strong>{buildingName}</strong> đã được <strong>hủy bỏ</strong> bởi chủ nhà.</p>
            <p>Bạn không cần thanh toán hóa đơn này. Nếu có thắc mắc, vui lòng liên hệ chủ nhà.</p>
            """)
    );

    // ── Payment ──

    public static (string Subject, string Html) PaymentRecorded(
        string tenantName, string roomNumber, string buildingName,
        int month, int year, decimal amount, decimal totalAmount, decimal totalPaid) =>
    (
        $"Xác nhận thanh toán — {amount:N0}₫",
        Wrap($"""
            <p>Xin chào <strong>{tenantName}</strong>,</p>
            <div class="success">
                <p>Thanh toán <span class="amount">{amount:N0}₫</span> đã được ghi nhận thành công.</p>
                <p><strong>Hóa đơn:</strong> Tháng {month}/{year} — {roomNumber}, {buildingName}</p>
                <p><strong>Tổng đã thanh toán:</strong> {totalPaid:N0}₫ / {totalAmount:N0}₫</p>
            </div>
            """)
    );

    // ── Contract ──

    public static (string Subject, string Html) ContractCreated(
        string tenantName, string roomNumber, string buildingName,
        DateOnly startDate, DateOnly endDate, decimal monthlyRent) =>
    (
        $"Hợp đồng mới — Phòng {roomNumber}, {buildingName}",
        Wrap($"""
            <p>Xin chào <strong>{tenantName}</strong>,</p>
            <p>Hợp đồng thuê phòng của bạn đã được tạo thành công.</p>
            <div class="info">
                <p><strong>Phòng:</strong> {roomNumber} — {buildingName}</p>
                <p><strong>Thời hạn:</strong> {startDate:dd/MM/yyyy} đến {endDate:dd/MM/yyyy}</p>
                <p><strong>Tiền thuê hàng tháng:</strong> <span class="amount">{monthlyRent:N0}₫</span></p>
            </div>
            """)
    );

    public static (string Subject, string Html) ContractRenewed(
        string tenantName, string roomNumber, string buildingName, DateOnly newEndDate) =>
    (
        $"Gia hạn hợp đồng — Phòng {roomNumber}",
        Wrap($"""
            <p>Xin chào <strong>{tenantName}</strong>,</p>
            <div class="success">
                <p>Hợp đồng phòng <strong>{roomNumber}</strong> tại <strong>{buildingName}</strong> đã được <strong>gia hạn</strong>.</p>
                <p><strong>Ngày kết thúc mới:</strong> {newEndDate:dd/MM/yyyy}</p>
            </div>
            """)
    );

    public static (string Subject, string Html) ContractTerminated(
        string tenantName, string roomNumber, string buildingName, decimal refundAmount) =>
    (
        $"Chấm dứt hợp đồng — Phòng {roomNumber}",
        Wrap($"""
            <p>Xin chào <strong>{tenantName}</strong>,</p>
            <p>Hợp đồng thuê phòng <strong>{roomNumber}</strong> tại <strong>{buildingName}</strong> đã được <strong>chấm dứt</strong>.</p>
            <div class="info">
                <p><strong>Hoàn cọc:</strong> <span class="amount">{refundAmount:N0}₫</span></p>
            </div>
            <p>Nếu có thắc mắc về khoản hoàn cọc, vui lòng liên hệ chủ nhà.</p>
            """)
    );

    // ── Contract Expiry Alert ──

    public static (string Subject, string Html) ContractExpiryTenant(
        string tenantName, string roomNumber, string buildingName, DateOnly endDate) =>
    (
        $"⚠ Hợp đồng sắp hết hạn — Phòng {roomNumber}",
        Wrap($"""
            <p>Xin chào <strong>{tenantName}</strong>,</p>
            <div class="warn">
                <p>Hợp đồng phòng <strong>{roomNumber}</strong> tại <strong>{buildingName}</strong> sẽ hết hạn vào ngày <strong>{endDate:dd/MM/yyyy}</strong>.</p>
            </div>
            <p>Vui lòng liên hệ chủ nhà để gia hạn hoặc sắp xếp trả phòng.</p>
            """)
    );

    public static (string Subject, string Html) ContractExpiryOwner(
        string ownerName, string tenantName, string roomNumber,
        string buildingName, DateOnly endDate) =>
    (
        $"⚠ Hợp đồng sắp hết hạn — {tenantName}, Phòng {roomNumber}",
        Wrap($"""
            <p>Xin chào <strong>{ownerName}</strong>,</p>
            <div class="warn">
                <p>Hợp đồng của khách <strong>{tenantName}</strong> tại phòng <strong>{roomNumber}</strong> ({buildingName}) sẽ hết hạn vào ngày <strong>{endDate:dd/MM/yyyy}</strong>.</p>
            </div>
            <p>Vui lòng liên hệ khách thuê để gia hạn hợp đồng hoặc chuẩn bị phòng cho khách mới.</p>
            """)
    );

    // ── Reservation ──

    public static (string Subject, string Html) ReservationExpired(
        string tenantName, string roomNumber, string buildingName) =>
    (
        $"Đặt phòng đã hết hạn — Phòng {roomNumber}",
        Wrap($"""
            <p>Xin chào <strong>{tenantName}</strong>,</p>
            <p>Đặt phòng <strong>{roomNumber}</strong> tại <strong>{buildingName}</strong> đã <strong>hết hạn</strong> và bị hủy tự động.</p>
            <p>Nếu bạn vẫn muốn thuê phòng, vui lòng liên hệ chủ nhà để đặt lại.</p>
            """)
    );

    // ── Maintenance Issue ──

    public static (string Subject, string Html) IssueCreated(
        string ownerName, string reporterName, string title,
        string buildingName, string? roomNumber) =>
    (
        $"Sự cố mới — {buildingName}",
        Wrap($"""
            <p>Xin chào <strong>{ownerName}</strong>,</p>
            <div class="warn">
                <p>Sự cố mới đã được báo cáo{(roomNumber != null ? $" tại phòng <strong>{roomNumber}</strong>" : "")} ({buildingName}):</p>
                <p><strong>Tiêu đề:</strong> {title}</p>
                <p><strong>Người báo cáo:</strong> {reporterName}</p>
            </div>
            """)
    );

    public static (string Subject, string Html) IssueStatusChanged(
        string reporterName, string title, string oldStatus, string newStatus) =>
    (
        $"Cập nhật sự cố — {title}",
        Wrap($"""
            <p>Xin chào <strong>{reporterName}</strong>,</p>
            <p>Sự cố "<strong>{title}</strong>" đã được cập nhật trạng thái:</p>
            <div class="info">
                <p><strong>{oldStatus}</strong> → <strong>{newStatus}</strong></p>
            </div>
            """)
    );
}
