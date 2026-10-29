using System;
using System.IO;
using ThermalPrintService.Models;

namespace ThermalPrintService.Services
{
    public class CrystalReportPrintService
    {
        private readonly ILogger<CrystalReportPrintService> _logger;
        private readonly IConfiguration _config;

        public CrystalReportPrintService(ILogger<CrystalReportPrintService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// สั่งพิมพ์ผ่าน Crystal Report (.rpt) โดยเจาะจงเครื่องพิมพ์ Thermal
        /// </summary>
        public PrintResult PrintRpt(string printerName, ThermalPrintRequest req)
        {
            try
            {
                string basePath = _config["PrintSettings:ReportBasePath"] ?? @"C:\DEVREPORT\Report_Web";
                string rptFileName = req.ReportFile ?? "VN_OUT_LABEL.rpt";
                string fullPath = Path.IsPathRooted(rptFileName) ? rptFileName : Path.Combine(basePath, rptFileName);

                if (!File.Exists(fullPath))
                {
                    return new PrintResult
                    {
                        Success = false,
                        Message = $"ไม่พบไฟล์ Report: {fullPath}",
                        TargetPrinter = printerName
                    };
                }

                _logger.LogInformation("กำลังโหลด Report: {FullPath} สำหรับเครื่องพิมพ์: {Printer}", fullPath, printerName);

                // ใช้ Reflection โหลด CrystalDecisions Runtime เพื่อรองรับเครื่องที่มี Crystal Reports ติดตั้งอยู่
                Type? reportDocType = Type.GetType("CrystalDecisions.CrystalReports.Engine.ReportDocument, CrystalDecisions.CrystalReports.Engine, Version=13.0.4000.0, Culture=neutral, PublicKeyToken=692fbea5521e1304")
                    ?? Type.GetType("CrystalDecisions.CrystalReports.Engine.ReportDocument, CrystalDecisions.CrystalReports.Engine");

                if (reportDocType == null)
                {
                    return new PrintResult
                    {
                        Success = false,
                        Message = "เครื่องแม่ข่ายยังไม่ได้ติดตั้ง SAP Crystal Reports Runtime for .NET",
                        TargetPrinter = printerName
                    };
                }

                dynamic? rpt = Activator.CreateInstance(reportDocType);
                if (rpt == null)
                {
                    return new PrintResult { Success = false, Message = "ไม่สามารถสร้างอินสแตนซ์ ReportDocument ได้" };
                }

                using (rpt as IDisposable)
                {
                    rpt.Load(fullPath);

                    // ตั้งค่า Database Logon หากมีการระบุไว้ใน appsettings.json
                    string dbUser = _config["PrintSettings:DatabaseLogon:UserID"] ?? "";
                    string dbPass = _config["PrintSettings:DatabaseLogon:Password"] ?? "";
                    string dbServer = _config["PrintSettings:DatabaseLogon:Server"] ?? "";
                    string dbName = _config["PrintSettings:DatabaseLogon:Database"] ?? "";
                    if (!string.IsNullOrEmpty(dbUser))
                    {
                        try
                        {
                            rpt.SetDatabaseLogon(dbUser, dbPass, dbServer, dbName);
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogWarning("SetDatabaseLogon: {Message}", dbEx.Message);
                        }
                    }

                    // ตั้งค่า Parameter
                    if (!string.IsNullOrEmpty(req.VN)) SafeSetParameter(rpt, "VN", req.VN);
                    if (!string.IsNullOrEmpty(req.HN)) SafeSetParameter(rpt, "HN", req.HN);
                    if (!string.IsNullOrEmpty(req.VisitDate)) SafeSetParameter(rpt, "VisitDate", req.VisitDate);
                    if (!string.IsNullOrEmpty(req.PatientName)) SafeSetParameter(rpt, "Name", req.PatientName);
                    if (!string.IsNullOrEmpty(req.CloseVisitCodeName)) SafeSetParameter(rpt, "CloseVisitCodeName", req.CloseVisitCodeName);

                    if (req.ExtraParams != null)
                    {
                        foreach (var kv in req.ExtraParams)
                        {
                            SafeSetParameter(rpt, kv.Key, kv.Value);
                        }
                    }

                    // กำหนดเครื่องพิมพ์ Thermal
                    rpt.PrintOptions.PrinterName = printerName;

                    // สั่งพิมพ์ออกเครื่องพิมพ์โดยตรง (Silent Print)
                    int copies = Math.Max(1, req.Copies);
                    rpt.PrintToPrinter(copies, false, 0, 0);
                }

                return new PrintResult
                {
                    Success = true,
                    Message = $"พิมพ์ไฟล์ {Path.GetFileName(fullPath)} ออกเครื่อง '{printerName}' สำเร็จ",
                    TargetPrinter = printerName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "เกิดข้อผิดพลาดในการพิมพ์ผ่าน Crystal Report: {Message}", ex.Message);
                return new PrintResult
                {
                    Success = false,
                    Message = $"เกิดข้อผิดพลาดในการพิมพ์ RPT: {ex.Message}",
                    TargetPrinter = printerName
                };
            }
        }

        private void SafeSetParameter(dynamic rpt, string paramName, object value)
        {
            try
            {
                rpt.SetParameterValue(paramName, value);
            }
            catch
            {
                // ถ้าใน report ไม่มีชื่อ parameter นี้ ให้ข้าม
            }
        }
    }
}

