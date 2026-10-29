# Thermal Print Service (.NET 8 Windows)

โปรเจกต์บริการสั่งพิมพ์สติกเกอร์ / ฉลาก (Thermal Printer) สำหรับระบบโรงพยาบาล (Web.DxFwHIS)
แยกงานพิมพ์ประเภทฉลากออกมาโดยเฉพาะ ไม่รบกวน Default Printer หรือเครื่องพิมพ์เอกสาร A4 ทั่วไป

---

## จุดเด่น
1. **เจาะจงเครื่องพิมพ์ได้ (Specific Printer):** สั่งพิมพ์ออกเครื่อง Thermal โดยตรง ไม่ต้องพึ่งพา Default Printer ของ Windows
2. **พิมพ์แบบ Silent (Direct Print):** ไม่ต้องมีหน้าต่าง Print Preview เด้งมากวนใจ
3. **รองรับ 2 รูปแบบ:**
   - **Custom Draw (วาดเอง):** วาด Text + Barcode Code 128 ด้วย C# Graphics คมชัด รวดเร็ว ปรับขนาดได้ละเอียดระดับมิลลิเมตร
   - **Crystal Report (.rpt):** โหลดไฟล์ `.rpt` จาก `C:\DEVREPORT\Report_Web` แล้วสั่ง Spool ออกเครื่อง Thermal ที่กำหนด
4. **REST API (CORS Enabled):** หน้าเว็บ Angular สามารถเรียกสั่งพิมพ์ได้โดยตรง

---

## โครงสร้างโฟลเดอร์
```text
ThermalPrintService/
├── appsettings.json                    # ไฟล์ตั้งค่า Port, พาธ Report, ชื่อเครื่องพิมพ์
├── ThermalPrintService.csproj           # โปรเจกต์ไฟล์ .NET 8 (Windows)
├── Program.cs                          # Minimal API endpoints & CORS
├── Models/
│   └── ThermalPrintRequest.cs          # Data Model รับค่าจาก Web
└── Services/
    ├── CustomLabelPrintService.cs      # บริการวาดฉลากเองด้วย C# Graphics + Barcode
    └── CrystalReportPrintService.cs    # บริการอ่านและพิมพ์ไฟล์ Crystal Report (.rpt)
```

---

## การตั้งค่า (`appsettings.json`)
```json
{
  "PrintSettings": {
    "Port": 5055,
    "ReportBasePath": "C:\\DEVREPORT\\Report_Web",
    "DefaultThermalPrinter": "TSC TTP-244 Pro",
    "PrinterMappings": {
      "OPD-ROOM-01": "Thermal_OPD_01",
      "OPD-ROOM-02": "Thermal_OPD_02",
      "DEFAULT": "TSC TTP-244 Pro"
    }
  }
}
```

---

## วิธี Compile และนำไปใช้งาน (บนเครื่อง Windows Server หรือ Client)
1. เปิด Command Prompt หรือ PowerShell ในโฟลเดอร์นี้:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o ./publish
   ```
2. จะได้ไฟล์ `ThermalPrintService.exe` อยู่ในโฟลเดอร์ `publish/`
3. ดับเบิ้ลคลิกเพื่อรัน หรือติดตั้งเป็น Windows Service ด้วย NSSM / Windows Service Manager:
   ```cmd
   sc create ThermalPrintService binPath= "C:\ThermalPrintService\ThermalPrintService.exe" start= auto
   net start ThermalPrintService
   ```

---

## ตัวอย่างการเรียกใช้งานจาก Angular (TypeScript)

แทนที่คำสั่ง `PrintVNLabelOut` เดิมใน Angular:

```typescript
PrintVNLabelOutDirect(copy: string = "1") {
  const payload = {
    ComputerLocation: this.auth.ComputerName,
    VN: this.maindata.VN,
    HN: this.maindata.HN,
    PatientName: this.maindata.PatientName,
    VisitDate: this.maindata.VisitDate || this.maindata.Visitdate,
    Clinic: this.maindata.Clinic,
    ClinicName: this.maindata.ClinicName,
    RightName: this.maindata.RightName,
    Copies: parseInt(copy, 10) || 1,
    ReportFile: "สมุดรายงานผลการตรวจสุขภาพ_CCC_A4.rpt" // หรือส่งชื่อไฟล์ rpt
  };

  // เรียกเข้า ThermalPrintService ที่รันอยู่ที่ Server หรือเครื่องลูกข่าย (Port 5055)
  const serviceUrl = "http://10.204.4.84:5055/api/print/vn-label"; 
  // หรือถ้าลงไว้ที่เครื่อง Client: "http://localhost:5055/api/print/vn-label"

  this.http.post<any>(serviceUrl, payload).subscribe({
    next: (res) => {
      this.msg.showSuccess(res.message || "พิมพ์สติกเกอร์สำเร็จ");
    },
    error: (err) => {
      this.msg.showError(err.error?.message || "ไม่สามารถติดต่อเครื่องพิมพ์สติกเกอร์ได้");
    }
  });
}
```

