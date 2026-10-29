using System;
using System.Drawing;
using System.Drawing.Printing;
using ThermalPrintService.Models;
using ZXing;
using ZXing.Windows.Compatibility;

namespace ThermalPrintService.Services
{
    public class CustomLabelPrintService
    {
        private readonly ILogger<CustomLabelPrintService> _logger;

        public CustomLabelPrintService(ILogger<CustomLabelPrintService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// สั่งพิมพ์ฉลาก VN Label โดยวาดกราฟิกและบาร์โค้ดลงบนเครื่องพิมพ์ Thermal โดยตรง
        /// </summary>
        public PrintResult PrintLabel(string printerName, ThermalPrintRequest req)
        {
            try
            {
                using var printDoc = new PrintDocument();
                printDoc.PrinterSettings.PrinterName = printerName;
                printDoc.PrinterSettings.Copies = (short)Math.Max(1, req.Copies);

                if (!printDoc.PrinterSettings.IsValid)
                {
                    return new PrintResult
                    {
                        Success = false,
                        Message = $"เครื่องพิมพ์ '{printerName}' ไม่ถูกต้องหรือไม่พบในระบบ",
                        TargetPrinter = printerName
                    };
                }

                // ขนาดกระดาษสติกเกอร์มาตรฐาน (เช่น 80mm x 50mm -> 315 x 197 ร้อยละนิ้ว)
                printDoc.DefaultPageSettings.PaperSize = new PaperSize("ThermalLabel", 315, 197);
                printDoc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

                printDoc.PrintPage += (sender, e) =>
                {
                    var g = e.Graphics;
                    if (g == null) return;

                    // ตั้งค่าคุณภาพกราฟิกสำหรับ Thermal Printer
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                    using var fontTitle = new Font("Tahoma", 11, FontStyle.Bold);
                    using var fontName = new Font("Tahoma", 11, FontStyle.Regular);
                    using var fontDetails = new Font("Tahoma", 9, FontStyle.Regular);
                    using var brush = new SolidBrush(Color.Black);
                    using var brushBlue = new SolidBrush(Color.FromArgb(0, 51, 153));

                    float x = 10;
                    float y = 10;

                    // รูปแบบ DX_VNOUTLABEL64:
                    // 1. CloseVisitCodeName
                    string closeVisitText = !string.IsNullOrEmpty(req.CloseVisitCodeName) ? req.CloseVisitCodeName : "ตรวจเสร็จสิ้น / Close Visit";
                    g.DrawString(closeVisitText, fontTitle, brushBlue, x, y);
                    y += 24;

                    // 2. {@Name}
                    string patName = !string.IsNullOrEmpty(req.PatientName) ? req.PatientName : $"HN: {req.HN}";
                    g.DrawString(patName, fontName, brush, x, y);
                    y += 22;

                    // 3. VN: {VN}  VisitDate: {@VisitDate}
                    string visitDateText = req.VisitDate ?? DateTime.Now.ToString("dd/MM/yyyy");
                    g.DrawString($"VN: {req.VN}  VisitDate: {visitDateText}", fontDetails, brush, x, y);
                    y += 20;

                    // 4. บาร์โค้ด VN (Code 128)
                    string barcodeData = !string.IsNullOrEmpty(req.VN) ? req.VN : (req.HN ?? "");
                    if (!string.IsNullOrEmpty(barcodeData))
                    {
                        try
                        {
                            var barcodeWriter = new BarcodeWriter
                            {
                                Format = BarcodeFormat.CODE_128,
                                Options = new ZXing.Common.EncodingOptions
                                {
                                    Height = 40,
                                    Width = 240,
                                    Margin = 0,
                                    PureBarcode = false
                                }
                            };

                            using var barcodeBitmap = barcodeWriter.Write(barcodeData);
                            g.DrawImage(barcodeBitmap, x + 10, y + 4);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Generate barcode error: {Error}", ex.Message);
                            g.DrawString($"*{barcodeData}*", fontNormal, brush, x + 40, y + 8);
                        }
                    }

                    e.HasMorePages = false;
                };

                printDoc.Print();

                _logger.LogInformation("พิมพ์สติกเกอร์ VN: {VN} สำเร็จ ไปที่: {Printer}", req.VN, printerName);
                return new PrintResult
                {
                    Success = true,
                    Message = $"สั่งพิมพ์ออกเครื่อง '{printerName}' เรียบร้อยแล้ว",
                    TargetPrinter = printerName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "เกิดข้อผิดพลาดในการพิมพ์สติกเกอร์: {Message}", ex.Message);
                return new PrintResult
                {
                    Success = false,
                    Message = $"เกิดข้อผิดพลาด: {ex.Message}",
                    TargetPrinter = printerName
                };
            }
        }
    }
}

