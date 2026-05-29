using Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Infrastructure.Pdf;

public sealed class ContractPdfService : IContractPdfService
{
    public byte[] Generate(ContractPdfData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(30);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Element(c => Header(c, data));
                page.Content().Element(c => Content(c, data));
                page.Footer().Element(Footer);
            });
        });

        return document.GeneratePdf();
    }

    private static void Header(IContainer container, ContractPdfData data)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold().FontSize(14);
            column.Item().AlignCenter().Text("Độc lập - Tự do - Hạnh phúc").Bold().FontSize(12);
            column.Item().PaddingVertical(10).AlignCenter().Text("HỢP ĐỒNG THUÊ PHÒNG").Bold().FontSize(16);
        });
    }

    private static void Content(IContainer container, ContractPdfData data)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(10);

            column.Item().Text("Hôm nay, hai bên chúng tôi gồm có:");
            
            column.Item().Text(text =>
            {
                text.Span("BÊN CHO THUÊ: ").Bold();
                text.Span(data.OwnerName);
            });
            
            column.Item().Text(text =>
            {
                text.Span("BÊN THUÊ: ").Bold();
                text.Span(data.TenantName);
            });
            
            column.Item().Text("Hai bên thống nhất ký kết hợp đồng thuê phòng với các điều khoản sau:");
            
            column.Item().Text($"Điều 1: Bên A đồng ý cho Bên B thuê phòng số {data.RoomNumber} tại Tòa nhà {data.BuildingName}.");
            column.Item().Text($"Điều 2: Thời hạn thuê từ ngày {data.StartDate:dd/MM/yyyy} đến ngày {data.EndDate:dd/MM/yyyy}.");
            column.Item().Text($"Điều 3: Giá thuê là {data.MonthlyRent:#,##0} ₫/tháng.");
            column.Item().Text($"Điều 4: Tiền cọc là {data.DepositAmount:#,##0} ₫.");
            
            if (!string.IsNullOrWhiteSpace(data.Note))
            {
                column.Item().Text($"Ghi chú: {data.Note}");
            }
            
            column.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().AlignCenter().Text("BÊN CHO THUÊ\n(Ký và ghi rõ họ tên)").Bold();
                row.RelativeItem().AlignCenter().Text("BÊN THUÊ\n(Ký và ghi rõ họ tên)").Bold();
            });
        });
    }

    private static void Footer(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1));
            text.CurrentPageNumber();
            text.Span(" / ");
            text.TotalPages();
        });
    }
}
