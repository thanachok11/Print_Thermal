using System.Drawing.Printing;
using ThermalPrintService.Models;
using ThermalPrintService.Services;

var builder = WebApplication.CreateBuilder(args);

// อ่านพอร์ตจาก appsettings หรือ default 5055
int port = builder.Configuration.GetValue<int>("PrintSettings:Port", 5055);
builder.WebHost.UseUrls($"http://*:{port}");

// ลงทะเบียน Services
builder.Services.AddSingleton<CustomLabelPrintService>();
builder.Services.AddSingleton<CrystalReportPrintService>();

// เปิด CORS เพื่อให้ Angular เรียกข้ามโดเมนได้
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// 1. ตรวจสอบสถานะและรายชื่อเครื่องพิมพ์
app.MapGet("/api/status", () =>
{
    var printers = new List<string>();
    foreach (string printer in PrinterSettings.InstalledPrinters)
    {
        printers.Add(printer);
    }
    return Results.Ok(new
    {
        Status = "Running",
        Service = "Thermal Print Service (HIS)",
        InstalledPrinters = printers,
        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
    });
});

// 2. ดูเฉพาะรายชื่อเครื่องพิมพ์ทั้งหมดใน Windows
app.MapGet("/api/printers", () =>
{
    var printers = new List<string>();
    foreach (string printer in PrinterSettings.InstalledPrinters)
    {
        printers.Add(printer);
    }
    return Results.Ok(printers);
});

// ฟังก์ชันหาเครื่องพิมพ์ที่ตรงกับ ComputerLocation หรือใช้ค่า Default
string ResolvePrinterName(IConfiguration config, ThermalPrintRequest req)
{
    if (!string.IsNullOrEmpty(req.PrinterName))
    {
        return req.PrinterName;
    }

    if (!string.IsNullOrEmpty(req.ComputerLocation))
    {
        string? mappedPrinter = config[$"PrintSettings:PrinterMappings:{req.ComputerLocation}"];
        if (!string.IsNullOrEmpty(mappedPrinter))
        {
            return mappedPrinter;
        }
    }

    return config["PrintSettings:DefaultThermalPrinter"] ?? "TSC TTP-244 Pro";
}

// 3. API พิมพ์ VN Label (วาดเองด้วย C# Graphics + Barcode 128)
app.MapPost("/api/print/thermal-custom", (ThermalPrintRequest req, CustomLabelPrintService customService, IConfiguration config) =>
{
    string targetPrinter = ResolvePrinterName(config, req);
    var result = customService.PrintLabel(targetPrinter, req);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// 4. API พิมพ์ผ่านไฟล์ Crystal Report (.rpt)
app.MapPost("/api/print/thermal-rpt", (ThermalPrintRequest req, CrystalReportPrintService rptService, IConfiguration config) =>
{
    string targetPrinter = ResolvePrinterName(config, req);
    var result = rptService.PrintRpt(targetPrinter, req);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// 5. API อเนกประสงค์ (สั่งพิมพ์ VN Label กลาง)
app.MapPost("/api/print/vn-label", (ThermalPrintRequest req, CustomLabelPrintService customService, CrystalReportPrintService rptService, IConfiguration config) =>
{
    string targetPrinter = ResolvePrinterName(config, req);

    // ถ้ามีการระบุไฟล์ Report ให้ลองพิมพ์ผ่าน RPT ก่อน
    if (!string.IsNullOrEmpty(req.ReportFile))
    {
        var rptResult = rptService.PrintRpt(targetPrinter, req);
        if (rptResult.Success) return Results.Ok(rptResult);
    }

    // หากไม่มีไฟล์หรือ RPT พิมพ์ไม่สำเร็จ ให้ใช้วิธีวาด Custom Label แทน
    var customResult = customService.PrintLabel(targetPrinter, req);
    return customResult.Success ? Results.Ok(customResult) : Results.BadRequest(customResult);
});

Console.WriteLine("=================================================");
Console.WriteLine($" [ThermalPrintService] เริ่มทำงานที่ Port: {port}");
Console.WriteLine(" พร้อมรองรับการพิมพ์สติกเกอร์ Thermal แบบเจาะจงเครื่องพิมพ์");
Console.WriteLine("=================================================");

app.Run();

