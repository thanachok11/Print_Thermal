using System.Collections.Generic;

namespace ThermalPrintService.Models
{
    public class ThermalPrintRequest
    {
        public string? ComputerLocation { get; set; }
        public string? PrinterName { get; set; }
        public string? VN { get; set; }
        public string? HN { get; set; }
        public string? PatientName { get; set; }
        public string? VisitDate { get; set; }
        public string? Clinic { get; set; }
        public string? ClinicName { get; set; }
        public string? Doctor { get; set; }
        public string? DoctorName { get; set; }
        public string? RightName { get; set; }
        public string? ReportFile { get; set; } = "VN_OUT_LABEL.rpt";
        public int Copies { get; set; } = 1;
        public Dictionary<string, string>? ExtraParams { get; set; }
    }

    public class PrintResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TargetPrinter { get; set; }
    }
}

