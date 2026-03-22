using Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Infrastructure.Pdf;

/// <summary>
/// Generates Vietnamese invoice PDFs using QuestPDF Community edition.
/// </summary>
public sealed class InvoicePdfService : IInvoicePdfService
{
    static InvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(InvoicePdfData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => Header(c, data));
                page.Content().Element(c => Content(c, data));
                page.Footer().Element(Footer);
            });
        });

        return document.GeneratePdf();
    }

    private static void Header(IContainer container, InvoicePdfData data)
    {
        container.Column(column =>
        {
            column.Spacing(4);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(data.BuildingName).Bold().FontSize(16);
                    col.Item().Text(data.BuildingAddress).FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"Chủ sở hữu: {data.OwnerName}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(160).AlignRight().Column(col =>
                {
                    col.Item().Text("HÓA ĐƠN").Bold().FontSize(20).FontColor(Colors.Blue.Darken2);
                    col.Item().Text($"Tháng {data.BillingMonth:D2}/{data.BillingYear}").FontSize(11);
                });
            });

            column.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void Content(IContainer container, InvoicePdfData data)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(10);

            // Invoice metadata
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Thông tin khách thuê").Bold().FontSize(11);
                    col.Item().Text($"Họ tên: {data.TenantName}");
                    col.Item().Text($"Phòng: {data.RoomNumber}");
                });
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("Thông tin hóa đơn").Bold().FontSize(11);
                    col.Item().Text($"Ngày tạo: {data.CreatedAt:dd/MM/yyyy}");
                    col.Item().Text($"Hạn thanh toán: {data.DueDate:dd/MM/yyyy}");
                    col.Item().Text($"Trạng thái: {TranslateStatus(data.Status)}");
                });
            });

            // Detail table
            column.Item().Element(c => DetailTable(c, data));

            // Totals
            column.Item().AlignRight().Width(280).Element(c => TotalsBlock(c, data));

            // Note
            if (!string.IsNullOrWhiteSpace(data.Note))
            {
                column.Item().PaddingTop(10).Column(col =>
                {
                    col.Item().Text("Ghi chú:").Bold();
                    col.Item().Text(data.Note).Italic().FontColor(Colors.Grey.Darken2);
                });
            }
        });
    }

    private static void DetailTable(IContainer container, InvoicePdfData data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);  // Service name
                columns.RelativeColumn(1);  // Unit
                columns.RelativeColumn(1);  // Old reading
                columns.RelativeColumn(1);  // New reading
                columns.RelativeColumn(1);  // Quantity
                columns.RelativeColumn(1.5f); // Unit price
                columns.RelativeColumn(1.5f); // Amount
            });

            // Header row
            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("Dịch vụ");
                header.Cell().Element(HeaderCellStyle).Text("Đơn vị");
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Chỉ số cũ");
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Chỉ số mới");
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Số lượng");
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Đơn giá");
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Thành tiền");
            });

            // Data rows
            foreach (var line in data.Details)
            {
                table.Cell().Element(DataCellStyle).Text(line.ServiceName);
                table.Cell().Element(DataCellStyle).Text(line.Unit);
                table.Cell().Element(DataCellStyle).AlignRight().Text(line.OldReading?.ToString("N0") ?? "—");
                table.Cell().Element(DataCellStyle).AlignRight().Text(line.NewReading?.ToString("N0") ?? "—");
                table.Cell().Element(DataCellStyle).AlignRight().Text(line.Quantity.ToString("N2"));
                table.Cell().Element(DataCellStyle).AlignRight().Text(FormatCurrency(line.UnitPrice));
                table.Cell().Element(DataCellStyle).AlignRight().Text(FormatCurrency(line.Amount));
            }
        });
    }

    private static void TotalsBlock(IContainer container, InvoicePdfData data)
    {
        container.Column(column =>
        {
            column.Spacing(2);

            TotalLine(column, "Tiền phòng", data.RentAmount);
            TotalLine(column, "Tiền dịch vụ", data.ServiceAmount);

            if (data.PenaltyAmount > 0)
                TotalLine(column, "Phí phạt", data.PenaltyAmount);

            if (data.DiscountAmount > 0)
                TotalLine(column, "Giảm giá", -data.DiscountAmount);

            column.Item().PaddingVertical(4).LineHorizontal(0.5f);

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("TỔNG CỘNG").Bold().FontSize(12);
                row.ConstantItem(120).AlignRight().Text(FormatCurrency(data.TotalAmount)).Bold().FontSize(12);
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Đã thanh toán");
                row.ConstantItem(120).AlignRight().Text(FormatCurrency(data.PaidAmount)).FontColor(Colors.Green.Darken2);
            });

            var remaining = data.TotalAmount - data.PaidAmount;
            if (remaining > 0)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Còn nợ").Bold().FontColor(Colors.Red.Darken2);
                    row.ConstantItem(120).AlignRight().Text(FormatCurrency(remaining)).Bold().FontColor(Colors.Red.Darken2);
                });
            }
        });
    }

    private static void TotalLine(ColumnDescriptor column, string label, decimal amount)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(label);
            row.ConstantItem(120).AlignRight().Text(FormatCurrency(amount));
        });
    }

    private static void Footer(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
            text.Span("ElysStay — Hệ thống quản lý cho thuê | Trang ");
            text.CurrentPageNumber();
            text.Span("/");
            text.TotalPages();
        });
    }

    private static IContainer HeaderCellStyle(IContainer container)
        => container
            .Background(Colors.Blue.Lighten4)
            .Padding(5)
            .DefaultTextStyle(x => x.Bold().FontSize(9));

    private static IContainer DataCellStyle(IContainer container)
        => container
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(4)
            .DefaultTextStyle(x => x.FontSize(9));

    private static string FormatCurrency(decimal amount)
        => amount.ToString("#,##0") + " ₫";

    private static string TranslateStatus(string status) => status switch
    {
        "Draft" => "Nháp",
        "Sent" => "Đã gửi",
        "PartiallyPaid" => "Thanh toán một phần",
        "Paid" => "Đã thanh toán",
        "Overdue" => "Quá hạn",
        "Void" => "Đã hủy",
        _ => status
    };
}
