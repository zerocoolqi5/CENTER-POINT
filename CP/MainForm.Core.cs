// -*- coding: utf-8 -*-
// Center Point - الوحدة الأساسية: سجل أخطاء مركزي + كشف جهاز موحّد — partial class
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CenterPoint
{
    // بطاقة تعريف الجهاز الموحّدة
    public class DevId
    {
        public string Serial = "";
        public string Mfr = "";
        public string Model = "";
        public string Device = "";
        public string Hardware = "";
        public string Platform = "";
        public string ChipFamily = "";   // qcom/mtk/spd/rk/allwinner/exynos/unknown
        public string Android = "";
        public string Sdk = "";
        public string Build = "";
        public string State = "none";     // system/recovery/sideload/fastboot/none
        public bool Present { get { return State != "none"; } }

        public string StateArabic()
        {
            switch (State)
            {
                case "system": return "يعمل طبيعي";
                case "recovery": return "Recovery";
                case "sideload": return "Recovery (Sideload)";
                case "fastboot": return "Fastboot / Bootloader";
                default: return "غير متصل / وضع خاص";
            }
        }
    }

    public partial class MainForm
    {
        static readonly object _logLock = new object();

        // ===== سجل مركزي موحّد =====
        // level: OP (عملية) / INFO / WARN / ERR
        void CpLog(string level, string msg)
        {
            try
            {
                string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string line = "[" + level + "] " + stamp + " | " + (msg ?? "");
                lock (_logLock)
                {
                    Directory.CreateDirectory(ExportsDir);
                    // كل شيء في السجل المركزي
                    File.AppendAllText(Path.Combine(ExportsDir, "center.log"), line + Environment.NewLine, new UTF8Encoding(false));
                    // توافق مع اللوحات الحالية
                    if (level == "ERR" || level == "WARN")
                        File.AppendAllText(Path.Combine(ExportsDir, "errors.log"), line + Environment.NewLine, new UTF8Encoding(false));
                    else
                        File.AppendAllText(Path.Combine(ExportsDir, "operations.log"), (msg ?? "") + "|" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch { /* السجل أفضل جهد — لا يوقف العمل */ }
        }

        // صندوق خطأ يسجّل مركزياً ثم يعرض للمستخدم
        void ErrBox(string msg)
        {
            CpLog("ERR", msg);
            try { MessageBox.Show(msg, "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch { }
        }

        void WarnLog(string msg) { CpLog("WARN", msg); }

        // ===== كشف الجهاز الموحّد =====
        // يفحص adb و fastboot ويرجّع بطاقة تعريف كاملة. آمن (قراءة فقط).
        DevId DeviceDetect()
        {
            var id = new DevId();
            try
            {
                string adb = (Shell.Run("adb", "devices", 15000).outp ?? "");
                string serial = "";
                foreach (var raw in adb.Split('\n'))
                {
                    string l = raw.Trim();
                    if (l == "" || l.StartsWith("List of")) continue;
                    var parts = l.Split('\t');
                    if (parts.Length < 2) parts = l.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    string st = parts[parts.Length - 1].Trim().ToLowerInvariant();
                    if (st == "device") { id.State = "system"; serial = parts[0]; break; }
                    if (st == "recovery") { id.State = "recovery"; serial = parts[0]; break; }
                    if (st == "sideload") { id.State = "sideload"; serial = parts[0]; break; }
                }
                if (id.State == "none")
                {
                    string fb = (Shell.Run("fastboot", "devices", 12000).outp ?? "");
                    if (fb.ToLowerInvariant().Contains("fastboot"))
                    {
                        id.State = "fastboot";
                        var first = fb.Split('\n').FirstOrDefault(x => x.ToLowerInvariant().Contains("fastboot"));
                        if (first != null) { var p = first.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries); if (p.Length > 0) serial = p[0]; }
                    }
                }
                id.Serial = serial;

                if (id.State == "system")
                {
                    var d = devices.FirstOrDefault(x => x.Kind == "android" && (serial == "" || x.Id == serial))
                            ?? devices.FirstOrDefault(x => x.Kind == "android");
                    if (d != null)
                    {
                        id.Mfr = Devices.Prop(d, "ro.product.manufacturer");
                        id.Model = Devices.Prop(d, "ro.product.model");
                        id.Device = Devices.Prop(d, "ro.product.device");
                        id.Platform = Devices.Prop(d, "ro.board.platform");
                        id.Hardware = Devices.Prop(d, "ro.hardware");
                        id.Android = Devices.Prop(d, "ro.build.version.release");
                        id.Sdk = Devices.Prop(d, "ro.build.version.sdk");
                        id.Build = Devices.Prop(d, "ro.build.display.id");
                        id.ChipFamily = FwChipFamily(id.Platform, id.Hardware);
                    }
                }
                else if (id.State == "fastboot" && serial != "")
                {
                    id.Model = FbVar(serial, "product");
                    id.Platform = FbVar(serial, "product");
                    id.ChipFamily = FwChipFamily(id.Platform, "");
                }
            }
            catch (Exception ex) { CpLog("ERR", "DeviceDetect: " + ex.Message); }
            return id;
        }

        static string FbVar(string serial, string var)
        {
            try
            {
                var r = Shell.Run("fastboot", "-s " + serial + " getvar " + var, 12000);
                string outp = (r.outp ?? "");
                foreach (var l in outp.Split('\n'))
                    if (l.ToLowerInvariant().Contains(var + ":"))
                        return l.Substring(l.IndexOf(':') + 1).Trim();
            }
            catch { }
            return "";
        }
    }
}
