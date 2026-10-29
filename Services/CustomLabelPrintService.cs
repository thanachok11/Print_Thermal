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

                    using var fontVN = new Font("Tahoma", 13, FontStyle.Bold);
                    using var fontHN = new Font("Tahoma", 10, FontStyle.Bold);
                    using var fontNormal = new Font("Tahoma", 8.5f, FontStyle.Regular);
                    using var fontSmall = new Font("Tahoma", 7.5f, FontStyle.Regular);
                    using var brush = new SolidBrush(Color.Black);

                    float x = 8;
                    float y = 8;

                    // 1. หัวเรื่อง VN และ วันที่
                    g.DrawString($"VN: {req.VN}", fontVN, brush, x, y);
                    g.DrawString($"Date: {req.VisitDate ?? DateTime.Now.ToString("dd/MM/yyyy HH:mm")}", fontSmall, brush, x + 160, y + 4);
                    y += 24;

                    // 2. ข้อมูลผู้ป่วย HN, ชื่อ-สกุล
                    g.DrawString($"HN: {req.HN}", fontHN, brush, x, y);
                    y += 18;
                    g.DrawString($"ชื่อ: {req.PatientName}", fontHN, brush, x, y);
                    y += 18;

                    // 3. สิทธิการรักษา และ แผนก/คลินิก
                    if (!string.IsNullOrEmpty(req.RightName))
                    {
                        g.DrawString($"สิทธิ: {req.RightName}", fontNormal, brush, x, y);
                        y += 16;
                    }

                    string clinicText = !string.IsNullOrEmpty(req.ClinicName) ? req.ClinicName : req.Clinic ?? "";
                    if (!string.IsNullOrEmpty(clinicText))
                    {
                        g.DrawString($"คลินิก: {clinicText}", fontNormal, brush, x, y);
                        y += 16;
                    }

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

