// -*- coding: utf-8 -*-
// Center Point - نقل وإدارة بيانات الهاتف (أندرويد + آيفون)
// سنتر بوينت للاتصالات - نسخة بواجهة احترافية (قائمة جانبية)
//
// الأدوات المطلوبة:  ADB للأندرويد  |  pymobiledevice3 + iTunes للآيفون
// البرنامج يعمل بدون إنترنت؛ النقل محلي عبر USB.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace CenterPoint
{
    class Device
    {
        public string Kind;   // "android" (ADB) | "ios" (Apple) | "mtp" (أندرويد بدون تصحيح USB)
        public string Id;
        public string Model;
        public override string ToString()
        {
            string tag = Kind == "ios" ? "🍎" : (Kind == "mtp" ? "📟" : "📱");
            if (Kind == "mtp") return tag + " " + Model + "  (MTP وسائط)";
            string sid = Id.Length > 12 ? Id.Substring(0, 12) : Id;
            return tag + " " + Model + "  (" + sid + ")";
        }
    }

    class Category
    {
        public string Name; public string Android; public string Ios;
        public Category(string n, string a, string i) { Name = n; Android = a; Ios = i; }
    }

    // ===== أدوات رسم احترافية (زوايا دائرية + تدرّجات) =====
    static class UI
    {
        public static GraphicsPath Round(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            if (rad <= 0 || r.Width <= 0 || r.Height <= 0) { p.AddRectangle(r); return p; }
            int d = rad * 2; if (d > r.Width) d = r.Width; if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // لوحة بزوايا دائرية مع تعبئة لونية أو تدرّج أفقي
    class RoundPanel : Panel
    {
        public int Radius = 14;
        public Color Fill1 = Color.FromArgb(22, 32, 58);
        public Color Fill2 = Color.Empty;        // إذا حُدّد: تدرّج
        public Color Border = Color.Empty;
        public RoundPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UI.Round(r, Radius))
            {
                if (Fill2 != Color.Empty)
                    using (var b = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), Fill1, Fill2, LinearGradientMode.ForwardDiagonal)) g.FillPath(b, path);
                else using (var b = new SolidBrush(Fill1)) g.FillPath(b, path);
                if (Border != Color.Empty) using (var pen = new Pen(Border, 1)) g.DrawPath(pen, path);
            }
            base.OnPaint(e);
        }
    }

    // شريط تقدّم بزوايا دائرية (بطارية/تخزين)
    class BarPanel : Panel
    {
        int _v = 0;
        public int Value { get { return _v; } set { _v = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public Color FillColor = Color.FromArgb(46, 160, 67);
        public Color Track = Color.FromArgb(38, 50, 78);
        public BarPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1); int rad = Height / 2;
            using (var tp = UI.Round(r, rad)) using (var b = new SolidBrush(Track)) g.FillPath(b, tp);
            int fw = (int)((Width - 1) * (_v / 100.0));
            if (fw > 2)
            {
                var fr = new Rectangle(0, 0, fw, Height - 1);
                using (var fp = UI.Round(fr, rad))
                using (var fb = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(2, fw), Height), FillColor, ControlPaint.Light(FillColor, 0.3f), LinearGradientMode.Horizontal))
                    g.FillPath(fb, fp);
            }
        }
    }

    static class Shell
    {
        // timeoutMs: مهلة زمنية تمنع تعليق البرنامج إذا توقف الأمر (مثلاً فصل الجهاز)
        public static (bool ok, string outp) Run(string file, string args, int timeoutMs = 120000)
        {
            if (file == "adb") file = AdbExe();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file, Arguments = args,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                using (var p = new Process())
                {
                    p.StartInfo = psi;
                    var sb = new StringBuilder();
                    object sync = new object();
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sync) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sync) sb.AppendLine(e.Data); };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(true); } catch { }
                        return (false, "انتهت المهلة الزمنية - تم إلغاء الأمر (تعليق محتمل).");
                    }
                    p.WaitForExit(); // ضمان إفراغ المخرجات
                    string outp; lock (sync) outp = sb.ToString();
                    return (p.ExitCode == 0, outp);
                }
            }
            catch (Exception ex) { return (false, "تعذّر تشغيل " + file + ": " + ex.Message); }
        }
        public static string Q(string s) { return "\"" + s + "\""; }

        // إيجاد adb تلقائياً حتى لو لم يكن في PATH (platform-tools في مجلد المستخدم مثلاً)
        static string _adbExe;
        public static string AdbExe()
        {
            if (_adbExe != null) return _adbExe;
            try
            {
                var cands = new List<string>();
                string up = Environment.GetEnvironmentVariable("USERPROFILE");
                string la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                if (!string.IsNullOrEmpty(up)) cands.Add(Path.Combine(up, "platform-tools", "adb.exe"));
                if (!string.IsNullOrEmpty(la)) cands.Add(Path.Combine(la, "Android", "Sdk", "platform-tools", "adb.exe"));
                if (!string.IsNullOrEmpty(up)) cands.Add(Path.Combine(up, "AppData", "Local", "Android", "Sdk", "platform-tools", "adb.exe"));
                try { cands.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "adb.exe")); } catch { }
                try { cands.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "platform-tools", "adb.exe")); } catch { }
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (var dir in path.Split(';')) { var t = dir.Trim(); if (t != "") { try { cands.Add(Path.Combine(t, "adb.exe")); } catch { } } }
                foreach (var c in cands) { try { if (File.Exists(c)) { _adbExe = c; return _adbExe; } } catch { } }
            }
            catch { }
            _adbExe = "adb";
            return _adbExe;
        }

        // تشغيل مع بثّ المخرجات سطراً سطراً (لشريط التقدّم) وإمكانية الإلغاء
        public static (bool ok, string outp) RunStream(string file, string args, Action<string> onLine, Func<bool> cancel, int timeoutMs = 3600000)
        {
            if (file == "adb") file = AdbExe();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file, Arguments = args,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                using (var p = new Process())
                {
                    p.StartInfo = psi;
                    var sb = new StringBuilder();
                    object sync = new object();
                    DataReceivedEventHandler h = (s, e) => { if (e.Data != null) { lock (sync) sb.AppendLine(e.Data); if (onLine != null) { try { onLine(e.Data); } catch { } } } };
                    p.OutputDataReceived += h; p.ErrorDataReceived += h;
                    p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    int waited = 0;
                    while (!p.WaitForExit(200))
                    {
                        waited += 200;
                        if (cancel != null && cancel()) { try { p.Kill(true); } catch { } return (false, "ألغيت العملية بواسطة المستخدم."); }
                        if (waited >= timeoutMs) { try { p.Kill(true); } catch { } return (false, "انتهت المهلة الزمنية."); }
                    }
                    p.WaitForExit();
                    string outp; lock (sync) outp = sb.ToString();
                    return (p.ExitCode == 0, outp);
                }
            }
            catch (Exception ex) { return (false, "تعذّر تشغيل " + file + ": " + ex.Message); }
        }
    }

    static class Devices
    {
        public static int Unauthorized, Offline;

        public static List<Device> ListAndroid()
        {
            var list = new List<Device>();
            Unauthorized = 0; Offline = 0;
            var r = Shell.Run("adb", "devices -l");
            if (!r.ok) return list;
            foreach (var line in r.outp.Replace("\r", "").Split('\n').Skip(1))
            {
                var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) continue;
                if (p[1] == "device")
                {
                    string model = p[0];
                    foreach (var x in p) if (x.StartsWith("model:")) model = x.Substring(6).Replace("_", " ");
                    var dev = new Device { Kind = "android", Id = p[0], Model = model };
                    try { dev.Model = AndroidNiceName(dev, model); } catch { }
                    list.Add(dev);
                }
                else if (p[1] == "unauthorized") Unauthorized++;
                else if (p[1] == "offline") Offline++;
            }
            return list;
        }

        // اسم موديل ودّي: الاسم التجاري + الشركة المصنّعة إن توفّرا، وإلا رمز الموديل
        public static string AndroidNiceName(Device d, string fallback)
        {
            string market = "";
            foreach (var key in new[] { "ro.product.marketname", "ro.product.vendor.marketname", "ro.config.marketing_name", "ro.product.odm.marketname" })
            {
                var v = Prop(d, key);
                if (!string.IsNullOrWhiteSpace(v) && v != "unknown") { market = v.Trim(); break; }
            }
            string maker = Prop(d, "ro.product.manufacturer");
            string name = !string.IsNullOrWhiteSpace(market) ? market : fallback;
            if (!string.IsNullOrWhiteSpace(maker) && maker != "unknown"
                && !name.ToLower().Contains(maker.ToLower()))
                name = char.ToUpper(maker[0]) + maker.Substring(1) + " " + name;
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        // انتظار عودة جهاز ADB بعد انقطاع قصير (يرجع true إذا عاد خلال المهلة)
        public static bool WaitForAdb(Device d, int seconds)
        {
            var until = DateTime.Now.AddSeconds(seconds);
            while (DateTime.Now < until)
            {
                var r = Shell.Run("adb", "devices", 10000);
                if (r.ok && Regex.IsMatch(r.outp, Regex.Escape(d.Id) + @"\s+device")) return true;
                System.Threading.Thread.Sleep(1200);
            }
            return false;
        }

        public static int CountRemote(Device d, string path)
        {
            var r = Shell.Run("adb", "-s " + d.Id + " shell find " + path + " -type f", 300000);
            if (!r.ok) return -1;
            return r.outp.Replace("\r", "").Split('\n').Count(l => l.Trim() != "");
        }

        public static List<Device> ListIos()
        {
            var list = new List<Device>();
            var seen = new HashSet<string>();
            // المسار الأول: pymobiledevice3
            var r = Shell.Run("pymobiledevice3", "usbmux list");
            if (r.ok)
                foreach (Match m in Regex.Matches(r.outp, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{16}|[0-9a-f]{40}"))
                    if (seen.Add(m.Value)) list.Add(new Device { Kind = "ios", Id = m.Value, Model = "iPhone / iPad" });
            // مسار احتياطي مع تعريفات Apple: libimobiledevice
            if (list.Count == 0)
            {
                var alt = Shell.Run("idevice_id", "-l");
                if (alt.ok)
                    foreach (var line in alt.outp.Replace("\r", "").Split('\n'))
                    {
                        string id = line.Trim();
                        if (id.Length >= 20 && id.Length <= 64 && Regex.IsMatch(id, @"^[0-9a-fA-F-]+$") && seen.Add(id))
                            list.Add(new Device { Kind = "ios", Id = id, Model = "iPhone / iPad" });
                    }
            }
            return list;
        }

        // أجهزة MTP (أندرويد بوضع «نقل الملفات» بدون تصحيح USB) — تظهر لويندوز كأجهزة محمولة
        public static List<Device> ListMtp()
        {
            var list = new List<Device>();
            try
            {
                dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                dynamic pc = sh.NameSpace(17); // This PC
                foreach (dynamic it in pc.Items())
                {
                    bool isF = false; string path = "", nm = "";
                    try { isF = it.IsFolder; path = (string)it.Path; nm = (string)it.Name; } catch { }
                    if (isF && path != null && path.StartsWith("::") && !string.IsNullOrEmpty(nm))
                        list.Add(new Device { Kind = "mtp", Id = "mtp:" + nm, Model = nm });
                }
            }
            catch { }
            return list;
        }

        public static List<Device> All()
        {
            var l = ListAndroid();
            l.AddRange(ListIos());
            try { l.AddRange(ListMtp()); } catch { }
            return l;
        }

        public static string Prop(Device d, string key)
        {
            var r = Shell.Run("adb", "-s " + d.Id + " shell getprop " + key);
            return r.ok ? r.outp.Trim() : "";
        }

        public static string Ram(Device d)
        {
            var r = Shell.Run("adb", "-s " + d.Id + " shell cat /proc/meminfo");
            if (!r.ok) return "";
            var m = Regex.Match(r.outp, @"MemTotal:\s*(\d+)\s*kB");
            if (!m.Success) return "";
            if (long.TryParse(m.Groups[1].Value, out long kb))
                return string.Format("{0:0.0} GB", kb / 1024.0 / 1024.0);
            return "";
        }

        public static int Battery(Device d)
        {
            var r = Shell.Run("adb", "-s " + d.Id + " shell dumpsys battery");
            if (!r.ok) return -1;
            var m = Regex.Match(r.outp, @"level:\s*(\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int lv)) return lv;
            return -1;
        }

        // (totalGB, usedGB, freeGB, pct)
        public static (double tot, double used, double free, int pct) Storage(Device d)
        {
            var r = Shell.Run("adb", "-s " + d.Id + " shell df /sdcard");
            if (!r.ok) return (0, 0, 0, 0);
            foreach (var ln in r.outp.Replace("\r", "").Split('\n'))
            {
                var p = ln.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4 && p[0] != "Filesystem")
                {
                    if (long.TryParse(p[1], out long tot) && long.TryParse(p[2], out long used) && long.TryParse(p[3], out long avail))
                    {
                        double g = 1024.0 * 1024.0;
                        int pct = tot > 0 ? (int)Math.Round(used * 100.0 / tot) : 0;
                        return (tot / g, used / g, avail / g, pct);
                    }
                }
            }
            return (0, 0, 0, 0);
        }
    }

    public partial class MainForm : Form
    {
        // ألوان الهوية
        static readonly Color BG = Color.FromArgb(15, 20, 36);
        static readonly Color SIDE = Color.FromArgb(9, 14, 30);
        static readonly Color GREEN = Color.FromArgb(46, 160, 67);
        static readonly Color BLUE = Color.FromArgb(31, 111, 235);
        static readonly Color CARD = Color.FromArgb(22, 32, 58);

        const string CONTACTS = "جهات الاتصال / الأسماء";

        List<Device> devices = new List<Device>();
        Panel sidebar, pageInfo, pageTransfer, pageGallery, pageBackup, pageApps, pageAccounts, pageMtp, pageUsb, pageFiles;
        // ملاحظة: pageProtect و pageReports مُعرّفتان أدناه
        // مدير الملفات
        ComboBox fmDev; TextBox fmPathBox; ListBox fmList; Label fmStatus; string fmPath = "/sdcard";
        ProgressBar fmProg; Button fmCancel; volatile bool fmCancelFlag; volatile bool fmBusy;
        // مسارات محميّة من الحذف (حماية من المسارات الخطرة)
        static readonly string[] FmProtected = { "/", "/sdcard", "/storage", "/storage/emulated", "/storage/emulated/0", "/system", "/vendor", "/data", "/proc", "/dev", "/mnt", "/sys", "/sdcard/Android", "/sdcard/DCIM" };
        Button biInfo, biTransfer, biGallery, biBackup, biApps, biAccounts, biMtp, biUsb, biFiles, biReports, biProtect, biRepair;
        Panel pageReports; ListBox repList; Label repSummary;
        // قسم حماية البيانات — تقرير الإصلاح (فحص وتشخيص فقط)
        Panel pageProtect; Button protScanBtn, protSaveBtn; TextBox protResult;
        // قسم السوفت وير — مركز شامل (معلومات + OTA + تطبيقات + تفليش + بحث فيرموير)
        Panel pageSoft; Button biSoft; TextBox softResult, softModel; string softOtaPath = "";
        // بحث الروم داخل Firmware Flashing
        TextBox flModel;
        // مكتبة الفيرموير — أرشفة ملفات التفليش المحفوظة
        Panel pageFirmware; Button biFirmware; ListBox fwList; TextBox fwDetail; Label fwCount; string fwFolder = ""; List<string> fwPaths = new List<string>();
        // الأداء — معلومات + مسح كاش + إدارة تطبيقات الخلفية (بدون مسح بيانات)
        Panel pagePerf; Button biPerf; ListBox perfList; TextBox perfResult; List<string> perfPkgs = new List<string>();
        // ADB Manager — أوامر ADB جاهزة
        Panel pageAdb; Button biAdb; TextBox adbResult, adbCmd, adbPkg;
        // مركز الإصلاح — أربعة تبويبات (كشف الحالة + إرشاد؛ بدون تفليش/فورمات تلقائي، بدون تجاوز FRP)
        Panel pageRepair, repTabRec, repTabMgr, repTabFl, repTabUnlock;
        Button repDetectBtn, repRebootBtn, repRecBtn, repFbBtn, repBtnRec, repBtnMgr, repBtnFl, repBtnUnlock;
        TextBox repairResult, repMgrResult, repFlResult, repFlPartition, repUnlockResult, repMgrModel;
        string repMgrPath = "", repFlPath = "";
        System.Windows.Forms.Timer monTimer; Button monBtn; Label monStatus; bool monOn; string monLast = "";
        System.Windows.Forms.Timer perfTimer; string baseTitle = "CENTER POINT TRANSFER V8 PRO - سنتر بوينت";

        // عناصر قسم فحص USB
        Button usbScanBtn, usbToolsBtn, usbSaveBtn;
        TextBox usbResult;

        // عناصر قسم MTP (بدون ADB)
        ComboBox mtpSrcCb, mtpDstCb;
        ListView mtpList;
        Label mtpPathLbl, mtpStatusLbl;
        Button mtpDetectBtn, mtpUpBtn, mtpMoveBtn;
        List<object> mtpDevices = new List<object>();   // أجهزة MTP (FolderItem)
        Stack<object> mtpNav = new Stack<object>();      // سجل التنقّل
        object mtpCurrent;                               // المجلد الحالي

        // دعم اللغتين
        static string Lang = "ar";
        List<(Control c, string ar, string en)> i18n = new List<(Control, string, string)>();
        Button biLang;

        // عناصر صفحة الحسابات
        ComboBox accDev;
        ListBox accList;
        Button accLoad;
        Label accStatus;

        // عناصر صفحة نقل التطبيقات
        ComboBox appSrc, appDst;
        CheckedListBox appList;
        Button appLoad, appMove;
        Label appStatus;
        TextBox appLog, appSearch;
        List<string> allPkgs = new List<string>();
        HashSet<string> appChecked = new HashSet<string>();

        // عناصر صفحة النسخ الاحتياطي
        ComboBox bkDev;
        Label bkFolderLbl;
        TextBox bkLog;
        ProgressBar bkBar;
        Button bkBackupBtn, bkRestoreBtn, bkPickBtn;
        string bkFolder = "";

        // عناصر صفحة المعرض
        ComboBox galSrc, galDst;
        FlowLayoutPanel galGrid;
        Label galStatus;
        Button galLoad, galMove;
        Dictionary<string, string> galCache = new Dictionary<string, string>(); // remote -> local
        HashSet<string> galSelected = new HashSet<string>();
        string galTmp;

        // عناصر صفحة المعلومات
        ComboBox cbDev;
        Label lbModel, lbMan, lbAndroid, lbCpu, lbRam, lbBat, lbSerial, lbStoreText, lblPhoneName, lbStoreLegend, lbSecurity, lbResolution;
        Panel infoPhone, storeSeg;
        long segSystem, segApps, segPhotos, segMedia, segOther, segUsedTotal;
        Label lblCurrent;

        // الصفحة الرئيسية الفاخرة عبر WebView2
        WebView2 webHome;
        bool webReady;
        Label webFallback;
        string CurrentPage = "home";
        Dictionary<string, string> dispToPkg = new Dictionary<string, string>();

        // عناصر صفحة النقل
        ComboBox cbSrc, cbDst;
        Panel phSrc, phDst;
        ProgressBar bar; Label lblPct; TextBox log;
        Button btnStart;
        HashSet<string> transferSel = new HashSet<string>();
        List<Button> transferToggles = new List<Button>();

        Category[] cats = new Category[]
        {
            new Category("الصور والفيديو (DCIM)", "/sdcard/DCIM", "/DCIM"),
            new Category("صور الشاشة (Pictures)", "/sdcard/Pictures", null),
            new Category("التنزيلات (Download)", "/sdcard/Download", null),
            new Category("الموسيقى (Music)", "/sdcard/Music", null),
            new Category("الفيديوهات (Movies)", "/sdcard/Movies", null),
            new Category("التسجيلات الصوتية (Recordings)", "/sdcard/Recordings", null),
            new Category("المستندات (Documents)", "/sdcard/Documents", null),
            new Category("واتساب (WhatsApp)", "/sdcard/Android/media/com.whatsapp/WhatsApp", null),
        };

        string AppDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        // مجلد بيانات دائم خارج مجلد البرنامج (لا يُحذف عند إزالة البرنامج)
        string ExportsDir { get { var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CenterPoint Transfer", "exports"); try { Directory.CreateDirectory(p); } catch { } return p; } }

        // تسجيل نص قابل للترجمة
        void Tr(Control c, string ar, string en) { i18n.Add((c, ar, en)); c.Text = Lang == "ar" ? ar : en; }
        void ApplyLanguage()
        {
            RightToLeft = Lang == "ar" ? RightToLeft.Yes : RightToLeft.No;
            foreach (var t in i18n) t.c.Text = Lang == "ar" ? t.ar : t.en;
        }
        void ToggleLang()
        {
            Lang = Lang == "ar" ? "en" : "ar";
            ApplyLanguage();
            biLang.Text = Lang == "ar" ? "English" : "عربي";
        }

        public MainForm()
        {
            Text = "CENTER POINT TRANSFER V8 PRO - سنتر بوينت";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = false;
            ClientSize = new Size(1040, 710);
            BackColor = BG;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildSidebar();
            BuildInfoPage();
            BuildTransferPage();
            BuildGalleryPage();
            BuildBackupPage();
            BuildAppsPage();
            BuildAccountsPage();
            BuildMtpPage();
            BuildUsbPage();
            BuildFilesPage();
            BuildReportsPage();
            BuildProtectPage();
            BuildRepairPage();
            BuildSoftPage();
            BuildFirmwarePage();
            BuildPerfPage();
            BuildAdbPage();
            BuildDashPage();
            BuildTicketsPage();
            BuildCustomersPage();
            BuildInventoryPage();
            BuildInvoicesPage();
            BuildSamsungPage();
            BuildFwPage();
            BuildSettingsPage();
            BuildHomePage();

            // تسجيل الأزرار الأساسية للترجمة
            Tr(btnStart, "▶  ابدأ النقل", "▶  Start Transfer");
            Tr(bkBackupBtn, "💾  نسخ احتياطي (الهاتف ← الكمبيوتر)", "💾  Backup (Phone → PC)");
            Tr(bkRestoreBtn, "↩  استعادة (الكمبيوتر ← الهاتف)", "↩  Restore (PC → Phone)");
            Tr(galLoad, "📥 تحميل الصور", "📥 Load Photos");
            Tr(galMove, "▶ نقل المحدد", "▶ Transfer Selected");
            Tr(appLoad, "📋 تحميل قائمة التطبيقات", "📋 Load Apps List");
            Tr(appMove, "▶ نقل المحدد", "▶ Transfer Selected");
            Tr(accLoad, "🔍 عرض الحسابات", "🔍 Show Accounts");

            ShowPage("home");
            MinimumSize = new Size(1056, 730);
            Resize += (s, e) => LayoutShell();
            Load += (s, e) => { LayoutShell(); Refresh2(); CheckUpdateStartup(); };

            // مراقبة الأداء والذاكرة — يعرض استهلاك الذاكرة في عنوان النافذة
            perfTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            perfTimer.Tick += (s, e) =>
            {
                try
                {
                    long ram = 0;
                    try { ram = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64; } catch { }
                    Text = baseTitle + "   |   🧠 الذاكرة: " + (ram / 1024 / 1024) + " MB";
                }
                catch { }
            };
            perfTimer.Start();
        }

        Panel[] AllPages()
        {
            return new[] { pageInfo, pageTransfer, pageGallery, pageBackup, pageApps, pageAccounts, pageMtp, pageUsb, pageFiles, pageReports };
        }

        void LayoutShell()
        {
            if (sidebar == null) return;
            int w = ClientSize.Width, h = ClientSize.Height;
            sidebar.Location = new Point(w - 180, 0);
            sidebar.Size = new Size(180, h);
            foreach (var p in AllPages()) if (p != null) p.Size = new Size(w - 180, h);
            if (webHome != null) webHome.Size = new Size(w - 180, h);
            if (webFallback != null) webFallback.Size = new Size(w - 180, h);
        }

        // ============ القائمة الجانبية ============
        void BuildSidebar()
        {
            sidebar = new Panel { Location = new Point(720, 0), Size = new Size(180, 660), BackColor = SIDE, AutoScroll = true };
            sidebar.Paint += (s, e) =>
            {
                var g = e.Graphics; var rc = ((Panel)s).ClientRectangle;
                using (var br = new LinearGradientBrush(rc, Color.FromArgb(14, 20, 40), Color.FromArgb(6, 10, 22), LinearGradientMode.Vertical)) g.FillRectangle(br, rc);
                using (var pen = new Pen(Color.FromArgb(40, 70, 130))) g.DrawLine(pen, rc.Right - 1, 0, rc.Right - 1, rc.Bottom);
            };
            Controls.Add(sidebar);

            string logoPath = Path.Combine(AppDir, "logo.png");
            if (File.Exists(logoPath))
            {
                try
                {
                    var pb = new PictureBox
                    {
                        Image = Image.FromFile(logoPath),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Location = new Point(15, 14), Size = new Size(150, 138), BackColor = SIDE,
                        Cursor = Cursors.Hand
                    };
                    pb.Click += (s, e) => ShowPage("home");
                    sidebar.Controls.Add(pb);
                }
                catch { }
            }

            // ===== لوحة التحكم =====
            biDash = MakeSideButton("📊  لوحة التحكم", 108);
            biDash.Click += (s, e) => ShowPage("dash");

            // ===== إدارة الأجهزة =====
            MakeSideHeader("إدارة الأجهزة", 152);
            biInfo = MakeSideButton("📱  معلومات الجهاز", 182);
            biInfo.Click += (s, e) => ShowPage("info");
            biGallery = MakeSideButton("🖼  معرض الصور", 226);
            biGallery.Click += (s, e) => ShowPage("gallery");
            biApps = MakeSideButton("📦  نقل التطبيقات", 270);
            biApps.Click += (s, e) => ShowPage("apps");
            biFiles = MakeSideButton("📂  مدير الملفات", 314);
            biFiles.Click += (s, e) => ShowPage("files");
            biAccounts = MakeSideButton("🔑  الحسابات", 358);
            biAccounts.Click += (s, e) => ShowPage("accounts");

            // ===== نقل البيانات / النسخ الاحتياطي =====
            biTransfer = MakeSideButton("🔄  نقل البيانات", 402);
            biTransfer.Click += (s, e) => ShowPage("transfer");
            biBackup = MakeSideButton("💾  النسخ الاحتياطي", 446);
            biBackup.Click += (s, e) => ShowPage("backup");

            // ===== USB / ADB / MTP =====
            MakeSideHeader("USB / ADB / MTP", 490);
            biUsb = MakeSideButton("🔌  فحص USB", 520);
            biUsb.Click += (s, e) => ShowPage("usb");
            biAdb = MakeSideButton("🤖  ADB Manager", 564);
            biAdb.Click += (s, e) => ShowPage("adb");
            biMtp = MakeSideButton("🔗  MTP (بدون ADB)", 608);
            biMtp.Click += (s, e) => ShowPage("mtp");

            // ===== تشخيص الأعطال =====
            MakeSideHeader("تشخيص الأعطال", 652);
            biRepair = MakeSideButton("🔧  مركز الإصلاح", 682);
            biRepair.Click += (s, e) => ShowPage("repair");
            biProtect = MakeSideButton("🛡  حماية البيانات", 726);
            biProtect.Click += (s, e) => ShowPage("protect");
            biPerf = MakeSideButton("⚡  الأداء", 770);
            biPerf.Click += (s, e) => ShowPage("perf");

            // ===== Firmware Manager =====
            MakeSideHeader("Firmware Manager", 814);
            biFw = MakeSideButton("🧰  Firmware & Recovery", 844);
            biFw.Click += (s, e) => ShowPage("fw");
            biSamsung = MakeSideButton("⚡  مركز التفليش", 888);
            biSamsung.Click += (s, e) => ShowPage("samsung");
            biFirmware = MakeSideButton("📚  مكتبة الفيرموير", 932);
            biFirmware.Click += (s, e) => ShowPage("firmware");
            biSoft = MakeSideButton("💿  السوفت وير", 976);
            biSoft.Click += (s, e) => ShowPage("soft");

            // ===== Smart Recovery / تقارير =====
            biSmartRec = MakeSideButton("🩺  Smart Recovery", 1020);
            biSmartRec.Click += (s, e) => { ShowPage("fw"); FwTab("rec"); };
            biReports = MakeSideButton("📊  تقارير الصيانة", 1064);
            biReports.Click += (s, e) => ShowPage("reports");

            // ===== الزبائن والفواتير =====
            MakeSideHeader("الزبائن والفواتير", 1108);
            biCust = MakeSideButton("👥  الزبائن", 1138);
            biCust.Click += (s, e) => ShowPage("customers");
            biInvoice = MakeSideButton("🧾  الفواتير", 1182);
            biInvoice.Click += (s, e) => ShowPage("invoices");
            biInv = MakeSideButton("📦  المخزون", 1226);
            biInv.Click += (s, e) => ShowPage("inventory");
            biTickets = MakeSideButton("🎫  تذاكر الصيانة", 1270);
            biTickets.Click += (s, e) => ShowPage("tickets");

            // ===== الإعدادات =====
            biSettings = MakeSideButton("⚙  الإعدادات", 1314);
            biSettings.Click += (s, e) => ShowPage("settings");

            Tr(biInfo, "📱  معلومات الجهاز", "📱  Device Info");
            Tr(biTransfer, "🔄  نقل البيانات", "🔄  Transfer Data");
            Tr(biGallery, "🖼  معرض الصور", "🖼  Photo Gallery");
            Tr(biBackup, "💾  النسخ الاحتياطي", "💾  Backup");
            Tr(biApps, "📦  نقل التطبيقات", "📦  App Transfer");
            Tr(biFiles, "📂  مدير الملفات", "📂  File Manager");
            Tr(biAccounts, "🔑  الحسابات", "🔑  Accounts");
            Tr(biMtp, "🔗  MTP (بدون ADB)", "🔗  MTP (No ADB)");
            Tr(biUsb, "🔌  فحص USB", "🔌  USB Check");
            Tr(biReports, "📊  تقارير الصيانة", "📊  Maintenance Reports");

            biLang = new Button { Text = "English", Location = new Point(35, 572), Size = new Size(110, 28), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            biLang.FlatAppearance.BorderColor = BLUE; biLang.Click += (s, e) => ToggleLang();
            sidebar.Controls.Add(biLang);

            var footer = new Label
            {
                Text = "CENTER POINT\nV8 PRO",
                ForeColor = Color.FromArgb(120, 140, 180), BackColor = SIDE,
                Location = new Point(10, 610), Size = new Size(160, 40),
                TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 8),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            sidebar.Controls.Add(footer);
        }

        Label MakeSideHeader(string text, int y)
        {
            var l = new Label
            {
                Text = text, Location = new Point(10, y), Size = new Size(162, 22),
                ForeColor = Color.FromArgb(90, 170, 240), BackColor = SIDE,
                TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes,
                Font = new Font("Arial", 8, FontStyle.Bold)
            };
            l.Paint += (s, e) =>
            {
                var lb = (Label)s; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                // شرطة نيون يمين العنوان (RTL)
                using (var br = new SolidBrush(Color.FromArgb(47, 180, 255))) g.FillRectangle(br, lb.Width - 3, 6, 3, 10);
                // خط فاصل أسفل
                using (var pen = new Pen(Color.FromArgb(34, 52, 92))) g.DrawLine(pen, 4, lb.Height - 2, lb.Width - 8, lb.Height - 2);
            };
            sidebar.Controls.Add(l);
            return l;
        }

        Button MakeSideButton(string text, int y)
        {
            var b = new Button
            {
                Text = text, Location = new Point(8, y), Size = new Size(164, 42),
                BackColor = SIDE, ForeColor = Color.FromArgb(205, 218, 240), FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 11),
                RightToLeft = RightToLeft.Yes, Padding = new Padding(0, 0, 14, 0), Tag = "off"
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 32, 62);
            b.Paint += (s, e) =>
            {
                var btn = (Button)s;
                bool on = (string)btn.Tag == "on";
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var pill = new Rectangle(3, 4, btn.Width - 6, btn.Height - 8);
                if (on)
                {
                    // توهّج خارجي خفيف
                    using (var gp2 = Rnd(new Rectangle(1, 2, btn.Width - 2, btn.Height - 4), 13))
                    using (var glow = new SolidBrush(Color.FromArgb(55, 40, 150, 255))) g.FillPath(glow, gp2);
                    // حبّة نيون متدرّجة
                    using (var gp = Rnd(pill, 11))
                    using (var br = new LinearGradientBrush(pill, Color.FromArgb(47, 180, 255), Color.FromArgb(20, 96, 224), LinearGradientMode.Horizontal))
                    {
                        g.FillPath(br, gp);
                        using (var pen = new Pen(Color.FromArgb(150, 120, 220, 255))) g.DrawPath(pen, gp);
                    }
                    // شريط مؤشّر يمين (RTL)
                    using (var ac = new SolidBrush(Color.FromArgb(150, 235, 255))) g.FillRectangle(ac, btn.Width - 7, 12, 3, btn.Height - 24);
                    TextRenderer.DrawText(g, btn.Text, new Font(btn.Font, FontStyle.Bold), new Rectangle(0, 0, btn.Width - 16, btn.Height), Color.White, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft);
                }
            };
            sidebar.Controls.Add(b);
            return b;
        }

        // مسار مستطيل بزوايا مدوّرة
        static GraphicsPath Rnd(Rectangle r, int rad)
        {
            var p = new GraphicsPath(); int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void SideActive(Button b, bool on) { b.Tag = on ? "on" : "off"; b.BackColor = on ? Color.FromArgb(31, 111, 235) : SIDE; b.Invalidate(); }

        void ShowPage(string name)
        {
            CurrentPage = name;
            // الرئيسية والنقل = واجهة ويب (WebView2)؛ البقية صفحات WinForms
            bool web = (name == "home" || name == "transfer");
            if (webHome != null) webHome.Visible = web;
            if (webFallback != null) webFallback.Visible = web && !webReady;
            pageInfo.Visible = (name == "info");
            pageTransfer.Visible = false; // استُبدلت بواجهة الويب
            pageGallery.Visible = (name == "gallery");
            pageBackup.Visible = (name == "backup");
            pageApps.Visible = (name == "apps");
            pageAccounts.Visible = (name == "accounts");
            pageMtp.Visible = (name == "mtp");
            pageUsb.Visible = (name == "usb");
            pageFiles.Visible = (name == "files");
            pageReports.Visible = (name == "reports");
            if (pageProtect != null) pageProtect.Visible = (name == "protect");
            if (pageRepair != null) pageRepair.Visible = (name == "repair");
            if (pageSoft != null) pageSoft.Visible = (name == "soft");
            if (pageFirmware != null) pageFirmware.Visible = (name == "firmware");
            if (pagePerf != null) pagePerf.Visible = (name == "perf");
            if (pageAdb != null) pageAdb.Visible = (name == "adb");
            if (pageDash != null) { pageDash.Visible = (name == "dash"); if (name == "dash") DashRefresh(); }
            if (pageTickets != null) pageTickets.Visible = (name == "tickets");
            if (pageCust != null) { pageCust.Visible = (name == "customers"); if (name == "customers") CustomersRefreshList(); }
            if (pageInv != null) { pageInv.Visible = (name == "inventory"); if (name == "inventory") InventoryRefreshList(); }
            if (pageInvoice != null) { pageInvoice.Visible = (name == "invoices"); if (name == "invoices") InvoicesRefreshList(); }
            if (pageSamsung != null) pageSamsung.Visible = (name == "samsung");
            if (pageFw != null) pageFw.Visible = (name == "fw");
            if (pageSettings != null) pageSettings.Visible = (name == "settings");
            SideActive(biInfo, name == "info");
            SideActive(biTransfer, name == "transfer");
            SideActive(biGallery, name == "gallery");
            SideActive(biBackup, name == "backup");
            SideActive(biApps, name == "apps");
            SideActive(biFiles, name == "files");
            SideActive(biAccounts, name == "accounts");
            SideActive(biMtp, name == "mtp");
            SideActive(biUsb, name == "usb");
            SideActive(biReports, name == "reports");
            if (biProtect != null) SideActive(biProtect, name == "protect");
            if (biRepair != null) SideActive(biRepair, name == "repair");
            if (biSoft != null) SideActive(biSoft, name == "soft");
            if (biFirmware != null) SideActive(biFirmware, name == "firmware");
            if (biPerf != null) SideActive(biPerf, name == "perf");
            if (biAdb != null) SideActive(biAdb, name == "adb");
            if (biDash != null) SideActive(biDash, name == "dash");
            if (biTickets != null) SideActive(biTickets, name == "tickets");
            if (biCust != null) SideActive(biCust, name == "customers");
            if (biInv != null) SideActive(biInv, name == "inventory");
            if (biInvoice != null) SideActive(biInvoice, name == "invoices");
            if (biSamsung != null) SideActive(biSamsung, name == "samsung");
            if (biFw != null) SideActive(biFw, name == "fw");
            if (biSettings != null) SideActive(biSettings, name == "settings");
            if (web) { PostWeb(JsonSerializer.Serialize(new { type = "view", name = name })); if (name == "transfer") SendXferDevices(); }
            if (name == "info") LoadInfo();
            if (name == "mtp") MtpDetect();
            if (name == "usb") UsbScan();
            if (name == "files") FmLoad();
            if (name == "reports") RepLoad();
        }

        // إرسال قائمة الأجهزة (مع المساحة) لواجهة النقل
        void SendXferDevices()
        {
            Task.Run(() =>
            {
                var list = new List<object>();
                foreach (var d in devices)
                {
                    if (d.Kind == "android")
                    {
                        string model = Devices.Prop(d, "ro.product.model"); if (model == "") model = d.Model;
                        var st = Devices.Storage(d);
                        list.Add(new { kind = "android", model = model, storeTotal = (int)Math.Round(st.tot), storeFree = (int)Math.Round(st.free), storePct = st.pct });
                    }
                    else if (d.Kind == "mtp") list.Add(new { kind = "mtp", model = d.Model + " (MTP)", storeTotal = 0, storeFree = 0, storePct = 0 });
                    else list.Add(new { kind = "ios", model = d.Model, storeTotal = 0, storeFree = 0, storePct = 0 });
                }
                PostWeb(JsonSerializer.Serialize(new { type = "xfer-devices", list = list }));
            });
        }

        // ============ صفحة معلومات الجهاز ============
        void BuildInfoPage()
        {
            pageInfo = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageInfo);

            var title = new Label
            {
                Text = "معلومات الجهاز والتخزين", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG,
                Location = new Point(20, 8), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Arial", 15, FontStyle.Bold)
            };
            pageInfo.Controls.Add(title);
            Tr(title, "معلومات الجهاز والتخزين", "Device Info & Storage");

            var lbl = new Label
            {
                Text = "الجهاز:", ForeColor = Color.White, BackColor = BG,
                Location = new Point(520, 42), Size = new Size(120, 26), TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Arial", 11)
            };
            pageInfo.Controls.Add(lbl);
            cbDev = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(150, 42), Size = new Size(360, 28), Font = new Font("Arial", 10) };
            cbDev.SelectedIndexChanged += (s, e) => { LoadInfo(); infoPhone.Invalidate(); };
            pageInfo.Controls.Add(cbDev);
            var rf = new Button { Text = "🔄", Location = new Point(70, 42), Size = new Size(70, 28), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            rf.FlatAppearance.BorderSize = 0; rf.Click += (s, e) => { Refresh2(); LoadInfo(); infoPhone.Invalidate(); };
            pageInfo.Controls.Add(rf);

            // رسمة الهاتف (أسلوب MobileTrans)
            infoPhone = new Panel { Location = new Point(314, 78), Size = new Size(88, 122), BackColor = BG };
            infoPhone.Paint += (s, e) => { try { DrawPhone(e.Graphics, ((Panel)s).ClientRectangle, cbDev != null && cbDev.SelectedIndex >= 0); } catch { } };
            pageInfo.Controls.Add(infoPhone);
            lblPhoneName = new Label { Text = "—", ForeColor = Color.White, BackColor = BG, Location = new Point(207, 202), Size = new Size(300, 24), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 11, FontStyle.Bold) };
            pageInfo.Controls.Add(lblPhoneName);

            var box = new Panel { Location = new Point(20, 232), Size = new Size(675, 284), BackColor = CARD };
            pageInfo.Controls.Add(box);
            lbModel = InfoRow(box, "الموديل", 6);
            lbMan = InfoRow(box, "الشركة المصنّعة", 36);
            lbAndroid = InfoRow(box, "نظام التشغيل", 66);
            lbCpu = InfoRow(box, "المعالج", 96);
            lbRam = InfoRow(box, "الذاكرة RAM", 126);
            lbBat = InfoRow(box, "البطارية", 156);
            lbSecurity = InfoRow(box, "تصحيح الأمان", 186);
            lbResolution = InfoRow(box, "دقة الشاشة", 216);
            lbSerial = InfoRow(box, "الرقم التسلسلي", 246);

            var sb = new Panel { Location = new Point(20, 524), Size = new Size(675, 132), BackColor = CARD };
            pageInfo.Controls.Add(sb);
            var st = new Label { Text = "مساحة التخزين", ForeColor = Color.FromArgb(158, 203, 255), BackColor = CARD, Location = new Point(15, 10), Size = new Size(645, 24), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 12, FontStyle.Bold) };
            sb.Controls.Add(st);
            storeSeg = new Panel { Location = new Point(20, 42), Size = new Size(635, 26), BackColor = Color.FromArgb(10, 15, 28) };
            storeSeg.Paint += (s, e) => DrawStoreSeg(e.Graphics, ((Panel)s).ClientRectangle);
            sb.Controls.Add(storeSeg);
            lbStoreLegend = new Label { Text = "", ForeColor = Color.FromArgb(200, 210, 230), BackColor = CARD, Location = new Point(15, 74), Size = new Size(645, 24), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9) };
            sb.Controls.Add(lbStoreLegend);
            lbStoreText = new Label { Text = "—", ForeColor = Color.White, BackColor = CARD, Location = new Point(20, 100), Size = new Size(635, 30), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 11) };
            sb.Controls.Add(lbStoreText);
        }

        static readonly (string key, Color col)[] SegDefs = new[]
        {
            ("النظام",     Color.FromArgb(31, 111, 235)),
            ("التطبيقات",  Color.FromArgb(220, 70, 70)),
            ("الصور",      Color.FromArgb(230, 185, 40)),
            ("الوسائط",    Color.FromArgb(150, 90, 200)),
            ("أخرى",       Color.FromArgb(120, 130, 150)),
        };

        void DrawStoreSeg(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            long total = segUsedTotal;
            using (var bg = new SolidBrush(Color.FromArgb(30, 40, 66)))
                g.FillRectangle(bg, r);
            if (total <= 0) return;
            long[] vals = { segSystem, segApps, segPhotos, segMedia, segOther };
            float x = r.X;
            for (int i = 0; i < vals.Length; i++)
            {
                if (vals[i] <= 0) continue;
                float w = (float)vals[i] / total * r.Width;
                using (var b = new SolidBrush(SegDefs[i].col))
                    g.FillRectangle(b, x, r.Y, w + 0.5f, r.Height);
                x += w;
            }
        }

        void RefreshSeg()
        {
            if (storeSeg == null) return;
            if (storeSeg.InvokeRequired) storeSeg.BeginInvoke(new Action(() => storeSeg.Invalidate()));
            else storeSeg.Invalidate();
        }

        static long DiskStat(string text, params string[] keys)
        {
            foreach (var k in keys)
            {
                var m = Regex.Match(text, Regex.Escape(k) + @"\s*:\s*(\d+)");
                if (m.Success && long.TryParse(m.Groups[1].Value, out long v) && v > 0) return v;
            }
            return 0;
        }

        Label InfoRow(Panel parent, string title, int y)
        {
            var t = new Label { Text = title, ForeColor = Color.FromArgb(150, 170, 210), BackColor = CARD, Location = new Point(430, y), Size = new Size(225, 30), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 11, FontStyle.Bold) };
            parent.Controls.Add(t);
            var v = new Label { Text = "—", ForeColor = Color.White, BackColor = CARD, Location = new Point(20, y), Size = new Size(400, 30), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 11) };
            parent.Controls.Add(v);
            return v;
        }

        void LoadInfo()
        {
            if (cbDev.SelectedIndex < 0 || cbDev.SelectedIndex >= devices.Count) return;
            var d = devices[cbDev.SelectedIndex];
            foreach (var l in new[] { lbModel, lbMan, lbAndroid, lbCpu, lbRam, lbBat }) l.Text = "...";
            lbSerial.Text = d.Id; lblPhoneName.Text = "...";
            lbStoreText.Text = "جاري القراءة..."; lbStoreLegend.Text = "";
            segSystem = segApps = segPhotos = segMedia = segOther = segUsedTotal = 0; RefreshSeg();
            Task.Run(() =>
            {
                if (d.Kind == "android")
                {
                    string model = Devices.Prop(d, "ro.product.model");
                    string man = Devices.Prop(d, "ro.product.manufacturer");
                    string ver = Devices.Prop(d, "ro.build.version.release");
                    string sdk = Devices.Prop(d, "ro.build.version.sdk");
                    string cpu = Devices.Prop(d, "ro.soc.model");
                    if (cpu == "") cpu = Devices.Prop(d, "ro.board.platform");
                    string ram = Devices.Ram(d);
                    int bat = Devices.Battery(d);
                    var st = Devices.Storage(d);
                    SetText(lblPhoneName, model == "" ? d.Model : model);
                    SetText(lbModel, model == "" ? d.Model : model);
                    SetText(lbMan, man == "" ? "—" : man);
                    SetText(lbAndroid, ver == "" ? "—" : ("أندرويد " + ver + (sdk != "" ? "  (API " + sdk + ")" : "")));
                    SetText(lbCpu, cpu == "" ? "—" : cpu);
                    SetText(lbRam, ram == "" ? "—" : ram);
                    SetText(lbBat, bat >= 0 ? (bat + "%") : "—");
                    string sec = Devices.Prop(d, "ro.build.version.security_patch");
                    SetText(lbSecurity, sec == "" ? "—" : sec);
                    var wsz = Shell.Run("adb", "-s " + d.Id + " shell wm size", 15000);
                    var mres = Regex.Match(wsz.outp, @"(\d{3,5})x(\d{3,5})");
                    SetText(lbResolution, mres.Success ? (mres.Groups[1].Value + " × " + mres.Groups[2].Value) : "—");
                    SetText(lbSerial, d.Id);
                    if (st.tot > 0)
                    {
                        SetText(lbStoreText, string.Format("المستخدم: {0:0.0} GB   |   المتبقي: {1:0.0} GB   |   الكلي: {2:0.0} GB   ({3}%)",
                            st.used, st.free, st.tot, st.pct));
                        var ds = Shell.Run("adb", "-s " + d.Id + " shell dumpsys diskstats", 60000);
                        long usedBytes = (long)(st.used * 1024.0 * 1024.0 * 1024.0);
                        if (ds.ok)
                        {
                            long sys = DiskStat(ds.outp, "System Size");
                            long app = DiskStat(ds.outp, "App Size", "Apps Size") + DiskStat(ds.outp, "App Data Size");
                            long pho = DiskStat(ds.outp, "Photos Size");
                            long med = DiskStat(ds.outp, "Videos Size") + DiskStat(ds.outp, "Audio Size");
                            long oth = DiskStat(ds.outp, "Other Size") + DiskStat(ds.outp, "Downloads Size");
                            long sum = sys + app + pho + med + oth;
                            if (sum > 0)
                            {
                                segSystem = sys; segApps = app; segPhotos = pho; segMedia = med; segOther = oth;
                                if (usedBytes > sum) { segOther += (usedBytes - sum); segUsedTotal = usedBytes; }
                                else segUsedTotal = sum;
                            }
                            else { segOther = usedBytes; segUsedTotal = usedBytes; }
                        }
                        else { segOther = usedBytes; segUsedTotal = usedBytes; }
                        RefreshSeg();
                        SetText(lbStoreLegend, string.Format("● النظام {0}    ● التطبيقات {1}    ● الصور {2}    ● الوسائط {3}    ● أخرى {4}",
                            FmtSize(segSystem), FmtSize(segApps), FmtSize(segPhotos), FmtSize(segMedia), FmtSize(segOther)));
                    }
                    else SetText(lbStoreText, "تعذّر قراءة المساحة");
                }
                else
                {
                    SetText(lblPhoneName, d.Model);
                    SetText(lbModel, d.Model);
                    SetText(lbMan, "Apple");
                    SetText(lbAndroid, "iOS (معلومات محدودة)");
                    SetText(lbCpu, "—"); SetText(lbRam, "—"); SetText(lbBat, "—");
                    SetText(lbSecurity, "—"); SetText(lbResolution, "—");
                    SetText(lbSerial, d.Id);
                    SetText(lbStoreText, "مساحة الآيفون غير متاحة بهذي النسخة");
                }
            });
        }

        void SetText(Label l, string t) { if (l.InvokeRequired) l.BeginInvoke(new Action(() => l.Text = t)); else l.Text = t; }
        void SetBar(ProgressBar b, int v) { if (b.InvokeRequired) b.BeginInvoke(new Action(() => b.Value = Math.Max(0, Math.Min(100, v)))); else b.Value = Math.Max(0, Math.Min(100, v)); }

        // ============ صفحة النقل ============
        void BuildTransferPage()
        {
            pageTransfer = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageTransfer);

            var title = new Label
            {
                Text = "نقل البيانات بين الأجهزة", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG,
                Location = new Point(20, 6), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Arial", 15, FontStyle.Bold)
            };
            pageTransfer.Controls.Add(title);
            Tr(title, "نقل البيانات بين الأجهزة", "Transfer Data Between Devices");

            var rf = new Button { Text = "🔄 تحديث", Location = new Point(300, 38), Size = new Size(115, 26), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            rf.FlatAppearance.BorderSize = 0; rf.Click += (s, e) => Refresh2();
            pageTransfer.Controls.Add(rf);

            // عرض الهاتفين: المصدر (يمين) والوجهة (يسار) مع سهم بينهما
            AddLbl(pageTransfer, "المصدر", 455, 42, 130, Color.FromArgb(158, 203, 255), 11, true);
            AddLbl(pageTransfer, "الوجهة", 130, 42, 130, Color.FromArgb(158, 203, 255), 11, true);
            phSrc = MakePhone(490, 70); pageTransfer.Controls.Add(phSrc);
            phDst = MakePhone(145, 70); pageTransfer.Controls.Add(phDst);
            var arrow = new Label { Text = "⇆", ForeColor = GREEN, BackColor = BG, Location = new Point(290, 110), Size = new Size(130, 46), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 26, FontStyle.Bold) };
            pageTransfer.Controls.Add(arrow);

            cbSrc = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(435, 198), Size = new Size(175, 26), Font = new Font("Arial", 9) };
            cbSrc.SelectedIndexChanged += (s, e) => phSrc.Invalidate();
            pageTransfer.Controls.Add(cbSrc);
            cbDst = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(105, 198), Size = new Size(175, 26), Font = new Font("Arial", 9) };
            cbDst.SelectedIndexChanged += (s, e) => phDst.Invalidate();
            pageTransfer.Controls.Add(cbDst);

            var box = new Panel { Location = new Point(20, 230), Size = new Size(675, 216), BackColor = CARD };
            pageTransfer.Controls.Add(box);
            AddLbl(box, "اختر البيانات المراد نقلها", 15, 2, 645, Color.FromArgb(158, 203, 255), 10, true);

            var items = new (string full, string shortN, string icon, bool def)[]
            {
                ("الصور والفيديو (DCIM)", "الصور والفيديو", "📷", true),
                ("صور الشاشة (Pictures)", "صور الشاشة", "🖼", false),
                ("التنزيلات (Download)", "التنزيلات", "⬇", false),
                ("الموسيقى (Music)", "الموسيقى", "🎵", false),
                ("الفيديوهات (Movies)", "الفيديوهات", "🎬", false),
                ("التسجيلات الصوتية (Recordings)", "التسجيلات", "🎙", false),
                ("المستندات (Documents)", "المستندات", "📄", false),
                ("واتساب (WhatsApp)", "واتساب", "💬", false),
                (CONTACTS, "جهات الاتصال", "👤", false),
            };
            for (int i = 0; i < items.Length; i++)
            {
                int col = i % 4, row = i / 4;
                int x = 12 + col * 166, y = 28 + row * 62;
                MakeToggle(box, items[i].full, items[i].shortN, items[i].icon, x, y, items[i].def);
            }
            // زر تحديد الكل / إلغاء الكل (يملأ الفراغ في الصف الأخير)
            var selAllT = new Button { Location = new Point(510, 152), Size = new Size(150, 52), Text = "✓ تحديد الكل", BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            selAllT.FlatAppearance.BorderSize = 0;
            bool allOn = false;
            selAllT.Click += (s, e) =>
            {
                allOn = !allOn;
                foreach (var tb in transferToggles)
                {
                    string nm = (string)tb.Tag;
                    if (allOn) { transferSel.Add(nm); tb.BackColor = GREEN; tb.FlatAppearance.BorderColor = GREEN; }
                    else { transferSel.Remove(nm); tb.BackColor = Color.FromArgb(15, 20, 36); tb.FlatAppearance.BorderColor = Color.FromArgb(43, 58, 94); }
                }
                selAllT.Text = allOn ? "✗ إلغاء الكل" : "✓ تحديد الكل";
            };
            box.Controls.Add(selAllT);
            Tr(selAllT, "✓ تحديد الكل", "✓ Select All");

            btnStart = new Button { Text = "▶  ابدأ النقل", Location = new Point(20, 454), Size = new Size(675, 40), BackColor = GREEN, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 13, FontStyle.Bold) };
            btnStart.FlatAppearance.BorderSize = 0; btnStart.Click += (s, e) => Start();
            pageTransfer.Controls.Add(btnStart);

            lblCurrent = new Label { Text = "", ForeColor = Color.FromArgb(158, 203, 255), BackColor = BG, Location = new Point(20, 500), Size = new Size(675, 22), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 10, FontStyle.Bold) };
            pageTransfer.Controls.Add(lblCurrent);

            bar = new ProgressBar { Location = new Point(90, 526), Size = new Size(605, 20), Maximum = 100 };
            pageTransfer.Controls.Add(bar);
            lblPct = new Label { Text = "0%", ForeColor = Color.FromArgb(93, 224, 106), BackColor = BG, Location = new Point(20, 526), Size = new Size(64, 20), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 12, FontStyle.Bold) };
            pageTransfer.Controls.Add(lblPct);

            log = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(93, 224, 106), Location = new Point(20, 552), Size = new Size(675, 150), Font = new Font("Consolas", 9) };
            pageTransfer.Controls.Add(log);
        }

        void SetCurrent(string t) { if (lblCurrent.InvokeRequired) { lblCurrent.BeginInvoke(new Action(() => lblCurrent.Text = t)); return; } lblCurrent.Text = t; }

        void MakeToggle(Panel box, string fullName, string shortName, string icon, int x, int y, bool sel)
        {
            var b = new Button
            {
                Location = new Point(x, y), Size = new Size(150, 52), Tag = fullName,
                BackColor = sel ? GREEN : Color.FromArgb(15, 20, 36), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Text = icon + "  " + shortName,
                Font = new Font("Arial", 10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter
            };
            b.FlatAppearance.BorderColor = sel ? GREEN : Color.FromArgb(43, 58, 94);
            if (sel) transferSel.Add(fullName);
            b.Click += (s, e) =>
            {
                string nm = (string)b.Tag;
                if (transferSel.Contains(nm)) { transferSel.Remove(nm); b.BackColor = Color.FromArgb(15, 20, 36); b.FlatAppearance.BorderColor = Color.FromArgb(43, 58, 94); }
                else { transferSel.Add(nm); b.BackColor = GREEN; b.FlatAppearance.BorderColor = GREEN; }
            };
            box.Controls.Add(b);
            transferToggles.Add(b);
        }

        Panel MakePhone(int x, int y)
        {
            var p = new Panel { Location = new Point(x, y), Size = new Size(84, 124), BackColor = BG };
            p.Paint += (s, e) =>
            {
                var pan = (Panel)s;
                bool connected = false;
                try { connected = (pan == phSrc) ? (cbSrc != null && cbSrc.SelectedIndex >= 0) : (cbDst != null && cbDst.SelectedIndex >= 0); }
                catch { }
                DrawPhone(e.Graphics, pan.ClientRectangle, connected);
            };
            return p;
        }

        void DrawPhone(Graphics g, Rectangle area, bool connected)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var body = new Rectangle(area.X + 6, area.Y + 2, area.Width - 12, area.Height - 8);
            Color edge = connected ? BLUE : Color.FromArgb(70, 80, 110);
            using (var path = Rounded(body, 12))
            {
                using (var fill = new SolidBrush(Color.FromArgb(18, 28, 52))) g.FillPath(fill, path);
                using (var pen = new Pen(edge, 3)) g.DrawPath(pen, path);
            }
            var screen = new Rectangle(body.X + 6, body.Y + 10, body.Width - 12, body.Height - 24);
            if (screen.Width > 2 && screen.Height > 2)
            {
                using (var sp = Rounded(screen, 6))
                using (var br = new LinearGradientBrush(screen, Color.FromArgb(31, 111, 235), Color.FromArgb(46, 160, 67), 45f))
                    g.FillPath(br, sp);
            }
            if (connected)
            {
                int cs = 22;
                var cc = new Rectangle(body.X + body.Width / 2 - cs / 2, body.Bottom - 30, cs, cs);
                using (var cb = new SolidBrush(GREEN)) g.FillEllipse(cb, cc);
                using (var wp = new Pen(Color.White, 2))
                    g.DrawLines(wp, new[] { new Point(cc.X + 6, cc.Y + 11), new Point(cc.X + 10, cc.Y + 15), new Point(cc.X + 16, cc.Y + 7) });
            }
        }

        GraphicsPath Rounded(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void AddLbl(Control parent, string text, int x, int y, int w) { AddLbl(parent, text, x, y, w, Color.White, 11, false); }
        void AddLbl(Control parent, string text, int x, int y, int w, Color c, int size, bool bold)
        {
            parent.Controls.Add(new Label { Text = text, ForeColor = c, BackColor = parent.BackColor, Location = new Point(x, y), Size = new Size(w, 26), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", size, bold ? FontStyle.Bold : FontStyle.Regular) });
        }

        CheckBox MakeCheck(Control parent, string text, bool chk, int cy)
        {
            var c = new CheckBox { Text = text, Checked = chk, ForeColor = Color.White, BackColor = parent.BackColor, Location = new Point(360, cy), Size = new Size(300, 20), TextAlign = ContentAlignment.MiddleRight, CheckAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 10) };
            parent.Controls.Add(c);
            return c;
        }

        // ============ صفحة معرض الصور ============
        void BuildGalleryPage()
        {
            pageGallery = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageGallery);

            var title = new Label
            {
                Text = "معرض الصور - تحديد ونقل", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG,
                Location = new Point(20, 14), Size = new Size(675, 30), TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Arial", 15, FontStyle.Bold)
            };
            pageGallery.Controls.Add(title);
            Tr(title, "معرض الصور - تحديد ونقل", "Photo Gallery - Select & Transfer");

            AddLbl(pageGallery, "من:", 560, 52, 130);
            galSrc = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(250, 52), Size = new Size(300, 28), Font = new Font("Arial", 10) };
            pageGallery.Controls.Add(galSrc);
            galLoad = new Button { Text = "📥 تحميل الصور", Location = new Point(40, 52), Size = new Size(200, 28), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            galLoad.FlatAppearance.BorderSize = 0; galLoad.Click += (s, e) => LoadGallery();
            pageGallery.Controls.Add(galLoad);

            AddLbl(pageGallery, "إلى:", 560, 88, 130);
            galDst = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(250, 88), Size = new Size(300, 28), Font = new Font("Arial", 10) };
            pageGallery.Controls.Add(galDst);
            galMove = new Button { Text = "▶ نقل المحدد", Location = new Point(40, 88), Size = new Size(200, 28), BackColor = GREEN, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            galMove.FlatAppearance.BorderSize = 0; galMove.Click += (s, e) => MoveSelected();
            pageGallery.Controls.Add(galMove);

            var gSelAll = new Button { Text = "✓ تحديد الكل", Location = new Point(40, 120), Size = new Size(110, 26), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9) };
            gSelAll.FlatAppearance.BorderColor = GREEN; gSelAll.Click += (s, e) => GallerySelect(true);
            pageGallery.Controls.Add(gSelAll);
            var gDesel = new Button { Text = "✗ إلغاء", Location = new Point(155, 120), Size = new Size(90, 26), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9) };
            gDesel.FlatAppearance.BorderColor = Color.Gray; gDesel.Click += (s, e) => GallerySelect(false);
            pageGallery.Controls.Add(gDesel);

            galStatus = new Label { Text = "اضغط «تحميل الصور» لعرض أحدث صور الجهاز (حتى 150 صورة).", ForeColor = Color.FromArgb(201, 162, 39), BackColor = BG, Location = new Point(255, 122), Size = new Size(440, 24), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9) };
            pageGallery.Controls.Add(galStatus);

            galGrid = new FlowLayoutPanel { Location = new Point(20, 152), Size = new Size(675, 488), BackColor = Color.FromArgb(6, 9, 15), AutoScroll = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            pageGallery.Controls.Add(galGrid);
            title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            galStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        }

        void LoadGallery()
        {
            if (galSrc.SelectedIndex < 0 || galSrc.SelectedIndex >= devices.Count) { MessageBox.Show("اختر جهاز المصدر."); return; }
            var d = devices[galSrc.SelectedIndex];
            if (d.Kind != "android") { MessageBox.Show("المعرض يدعم أندرويد بهذي النسخة."); return; }
            galGrid.Controls.Clear(); galSelected.Clear(); galCache.Clear();
            galLoad.Enabled = false; galLoad.Text = "جاري التحميل...";
            SetGalStatus("جاري البحث عن الصور...");
            if (galTmp == null) { galTmp = Path.Combine(Path.GetTempPath(), "cp_gal"); }
            try { Directory.CreateDirectory(galTmp); } catch { }
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + d.Id + " shell find /sdcard/DCIM /sdcard/Pictures -type f");
                var imgs = new List<string>();
                if (r.ok)
                    foreach (var ln in r.outp.Replace("\r", "").Split('\n'))
                    {
                        string p = ln.Trim();
                        string lp = p.ToLower();
                        if (lp.EndsWith(".jpg") || lp.EndsWith(".jpeg") || lp.EndsWith(".png"))
                            imgs.Add(p);
                    }
                imgs.Sort(); imgs.Reverse();            // الأحدث أولاً (أسماء زمنية)
                if (imgs.Count > 150) imgs = imgs.GetRange(0, 150);
                SetGalStatus("تحميل " + imgs.Count + " صورة...");
                int n = 0;
                foreach (var remote in imgs)
                {
                    string local = Path.Combine(galTmp, n + "_" + Path.GetFileName(remote));
                    var pr = Shell.Run("adb", "-s " + d.Id + " pull " + Shell.Q(remote) + " " + Shell.Q(local));
                    if (pr.ok && File.Exists(local))
                    {
                        galCache[remote] = local;
                        AddThumb(remote, local);
                    }
                    n++;
                    if (n % 10 == 0) SetGalStatus("تم تحميل " + n + " / " + imgs.Count);
                }
                SetGalStatus("جاهز: " + galCache.Count + " صورة. اضغط على الصور لتحديدها ثم «نقل المحدد».");
                if (galLoad.InvokeRequired) galLoad.BeginInvoke(new Action(() => { galLoad.Enabled = true; galLoad.Text = "📥 تحميل الصور"; }));
            });
        }

        void AddThumb(string remote, string local)
        {
            if (galGrid.InvokeRequired) { galGrid.BeginInvoke(new Action(() => AddThumb(remote, local))); return; }
            try
            {
                var holder = new Panel { Size = new Size(108, 108), BackColor = Color.FromArgb(6, 9, 15), Margin = new Padding(4), Tag = remote };
                var pb = new PictureBox { Size = new Size(100, 100), Location = new Point(4, 4), SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand, Tag = remote };
                using (var img = Image.FromFile(local))
                    pb.Image = new Bitmap(img, new Size(100, 100));
                PaintEventHandler border = (s, e) =>
                {
                    if (galSelected.Contains(remote))
                        e.Graphics.DrawRectangle(new Pen(GREEN, 4), 0, 0, holder.Width - 1, holder.Height - 1);
                };
                holder.Paint += border;
                pb.Click += (s, e) =>
                {
                    if (galSelected.Contains(remote)) galSelected.Remove(remote);
                    else galSelected.Add(remote);
                    holder.Invalidate();
                    SetGalStatus("المحدد: " + galSelected.Count + " صورة");
                };
                holder.Controls.Add(pb);
                galGrid.Controls.Add(holder);
            }
            catch { }
        }

        void SetGalStatus(string t) { if (galStatus.InvokeRequired) galStatus.BeginInvoke(new Action(() => galStatus.Text = t)); else galStatus.Text = t; }

        void GallerySelect(bool sel)
        {
            galSelected.Clear();
            if (sel) foreach (var k in galCache.Keys) galSelected.Add(k);
            foreach (Control c in galGrid.Controls) c.Invalidate();
            SetGalStatus("المحدد: " + galSelected.Count + " صورة");
        }

        void MoveSelected()
        {
            if (galSelected.Count == 0) { MessageBox.Show("حدّد صورة واحدة على الأقل (اضغط عليها)."); return; }
            if (galDst.SelectedIndex < 0 || galDst.SelectedIndex >= devices.Count) { MessageBox.Show("اختر جهاز الوجهة."); return; }
            var d = devices[galDst.SelectedIndex];
            if (galSrc.SelectedIndex == galDst.SelectedIndex) { MessageBox.Show("المصدر والوجهة لازم مختلفين."); return; }
            if (d.Kind == "ios" && !ConfirmIosWrite()) return;
            var picks = galSelected.ToList();
            galMove.Enabled = false; galMove.Text = "جاري النقل...";
            Task.Run(() =>
            {
                int ok = 0, fail = 0, i = 0;
                foreach (var remote in picks)
                {
                    i++;
                    if (!galCache.ContainsKey(remote)) { fail++; continue; }
                    string local = galCache[remote];
                    string name = Path.GetFileName(remote);
                    bool done;
                    if (d.Kind == "android")
                    {
                        Shell.Run("adb", "-s " + d.Id + " shell mkdir -p /sdcard/DCIM/CenterPoint");
                        var pr = Shell.Run("adb", "-s " + d.Id + " push " + Shell.Q(local) + " /sdcard/DCIM/CenterPoint/" + name);
                        done = pr.ok;
                    }
                    else
                    {
                        var pr = Shell.Run("pymobiledevice3", "afc push " + Shell.Q(local) + " /DCIM/" + name + " --udid " + d.Id);
                        done = pr.ok;
                    }
                    if (done) ok++; else fail++;
                    SetGalStatus("نقل الصور المحددة... " + i + " / " + picks.Count + "  (نجح " + ok + "، فشل " + fail + ")");
                }
                SetGalStatus("✅ انتهى نقل الصور المحددة: نجح " + ok + "، فشل " + fail + ".");
                if (galMove.InvokeRequired) galMove.BeginInvoke(new Action(() => { galMove.Enabled = true; galMove.Text = "▶ نقل المحدد"; }));
            });
        }

        // ============ صفحة النسخ الاحتياطي والاستعادة ============
        // أقسام النسخ: اسم المجلد على القرص -> مسار الجهاز
        static readonly (string disk, string dev)[] BK =
        {
            ("DCIM", "/sdcard/DCIM"),
            ("Pictures", "/sdcard/Pictures"),
            ("Download", "/sdcard/Download"),
            ("Music", "/sdcard/Music"),
            ("Movies", "/sdcard/Movies"),
            ("WhatsApp", "/sdcard/Android/media/com.whatsapp/WhatsApp"),
        };

        void BuildBackupPage()
        {
            pageBackup = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageBackup);

            var title = new Label { Text = "النسخ الاحتياطي والاستعادة", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 14), Size = new Size(675, 30), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageBackup.Controls.Add(title);
            Tr(title, "النسخ الاحتياطي والاستعادة", "Backup & Restore");

            AddLbl(pageBackup, "الجهاز:", 560, 52, 130);
            bkDev = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(250, 52), Size = new Size(300, 28), Font = new Font("Arial", 10) };
            pageBackup.Controls.Add(bkDev);
            var rf = new Button { Text = "🔄", Location = new Point(40, 52), Size = new Size(70, 28), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            rf.FlatAppearance.BorderSize = 0; rf.Click += (s, e) => Refresh2();
            pageBackup.Controls.Add(rf);

            bkPickBtn = new Button { Text = "📁 اختر مجلد النسخ", Location = new Point(395, 92), Size = new Size(300, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            bkPickBtn.FlatAppearance.BorderColor = BLUE; bkPickBtn.Click += (s, e) => PickBackupFolder();
            pageBackup.Controls.Add(bkPickBtn);
            bkFolderLbl = new Label { Text = "لم يتم اختيار مجلد بعد", ForeColor = Color.FromArgb(201, 162, 39), BackColor = BG, Location = new Point(20, 94), Size = new Size(365, 26), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9) };
            pageBackup.Controls.Add(bkFolderLbl);

            bkBackupBtn = new Button { Text = "💾  نسخ احتياطي (الهاتف ← الكمبيوتر)", Location = new Point(365, 134), Size = new Size(330, 44), BackColor = GREEN, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 11, FontStyle.Bold) };
            bkBackupBtn.FlatAppearance.BorderSize = 0; bkBackupBtn.Click += (s, e) => StartBackup();
            pageBackup.Controls.Add(bkBackupBtn);
            bkRestoreBtn = new Button { Text = "↩  استعادة (الكمبيوتر ← الهاتف)", Location = new Point(20, 134), Size = new Size(330, 44), BackColor = Color.FromArgb(155, 89, 182), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 11, FontStyle.Bold) };
            bkRestoreBtn.FlatAppearance.BorderSize = 0; bkRestoreBtn.Click += (s, e) => StartRestore();
            pageBackup.Controls.Add(bkRestoreBtn);

            bkBar = new ProgressBar { Location = new Point(20, 190), Size = new Size(675, 20), Maximum = 100 };
            pageBackup.Controls.Add(bkBar);

            bkLog = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(93, 224, 106), Location = new Point(20, 220), Size = new Size(675, 420), Font = new Font("Consolas", 9) };
            pageBackup.Controls.Add(bkLog);
        }

        void BkLog(string m) { if (bkLog.InvokeRequired) { bkLog.BeginInvoke(new Action(() => BkLog(m))); return; } bkLog.AppendText(m + "\r\n"); }
        void BkProg(double p) { if (bkBar.InvokeRequired) { bkBar.BeginInvoke(new Action(() => BkProg(p))); return; } bkBar.Value = (int)Math.Max(0, Math.Min(100, p)); }

        void PickBackupFolder()
        {
            using (var fb = new FolderBrowserDialog())
            {
                fb.Description = "اختر مجلداً لحفظ/قراءة النسخة الاحتياطية";
                if (fb.ShowDialog() == DialogResult.OK) { bkFolder = fb.SelectedPath; bkFolderLbl.Text = bkFolder; }
            }
        }

        void StartBackup()
        {
            if (bkDev.SelectedIndex < 0 || bkDev.SelectedIndex >= devices.Count) { MessageBox.Show("اختر الجهاز."); return; }
            if (bkFolder == "" || !Directory.Exists(bkFolder)) { MessageBox.Show("اختر مجلد النسخ أول."); return; }
            var d = devices[bkDev.SelectedIndex];
            if (d.Kind == "ios") { IosBackup(d); return; }
            if (!BeginOp("النسخ الاحتياطي/الاستعادة")) return;
            bkBackupBtn.Enabled = false; bkRestoreBtn.Enabled = false;
            Task.Run(() =>
            {
                try
                {
                    string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
                    string model = Devices.Prop(d, "ro.product.model"); if (model == "") model = "device";
                    model = Regex.Replace(model, @"[^\w]", "_");
                    string root = Path.Combine(bkFolder, "CenterPoint_Backup_" + model + "_" + stamp);
                    Directory.CreateDirectory(root);
                    var t0 = DateTime.Now;
                    BkLog(""); BkLog("بدء النسخ الاحتياطي لـ " + d.Model + "  (" + t0.ToString("HH:mm:ss") + ")");
                    BkLog("المجلد: " + root);
                    int i = 0, okFiles = 0, warns = 0;
                    foreach (var b in BK)
                    {
                        i++;
                        BkProg((double)(i - 1) / BK.Length * 100);
                        var chk = Shell.Run("adb", "-s " + d.Id + " shell ls " + b.dev);
                        if (!chk.ok || chk.outp.Contains("No such file")) { BkLog("── " + b.disk + " ── غير موجود، تخطّي."); continue; }
                        BkLog("── " + b.disk + " ── جاري النسخ...");
                        string localDir = Path.Combine(root, b.disk);
                        int srcCount = Devices.CountRemote(d, b.dev);
                        var pr = Shell.Run("adb", "-s " + d.Id + " pull " + b.dev + " " + Shell.Q(localDir), 3600000);
                        int locCount = Directory.Exists(localDir) ? Directory.GetFiles(localDir, "*", SearchOption.AllDirectories).Length : 0;
                        okFiles += locCount;
                        if (!pr.ok) { BkLog("  ✗ فشل: " + Trim(pr.outp)); warns++; }
                        else if (srcCount >= 0 && srcCount != locCount) { BkLog("  ⚠ تحقق: المصدر " + srcCount + " ، المنسوخ " + locCount); warns++; }
                        else BkLog("  ✔ تم — " + locCount + " ملف");
                    }
                    // جهات الاتصال
                    BkLog("── جهات الاتصال ──");
                    var cr = Shell.Run("adb", "-s " + d.Id + " shell content query --uri content://com.android.contacts/data --where " + Shell.Q("mimetype='vnd.android.cursor.item/phone_v2'") + " --projection display_name:data1");
                    if (cr.ok)
                    {
                        var people = new Dictionary<string, List<string>>();
                        foreach (var line in cr.outp.Replace("\r", "").Split('\n'))
                        {
                            var m = Regex.Match(line, @"display_name=(.*?),\s*data1=(.*?)\s*$");
                            if (!m.Success) continue;
                            string nm = m.Groups[1].Value.Trim(), nu = m.Groups[2].Value.Trim();
                            if (nm == "" || nu == "" || nm == "null") continue;
                            if (!people.ContainsKey(nm)) people[nm] = new List<string>();
                            if (!people[nm].Contains(nu)) people[nm].Add(nu);
                        }
                        if (people.Count > 0)
                        {
                            var sb = new StringBuilder();
                            foreach (var kv in people) { sb.AppendLine("BEGIN:VCARD"); sb.AppendLine("VERSION:3.0"); sb.AppendLine("FN:" + kv.Key); foreach (var n in kv.Value) sb.AppendLine("TEL;TYPE=CELL:" + n); sb.AppendLine("END:VCARD"); }
                            File.WriteAllText(Path.Combine(root, "Contacts.vcf"), sb.ToString(), new UTF8Encoding(false));
                            BkLog("  ✔ " + people.Count + " اسم");
                        }
                        else BkLog("  لا أسماء");
                    }
                    BkProg(100);
                    var dur = (DateTime.Now - t0);
                    BkLog("");
                    BkLog("✅ انتهى النسخ الاحتياطي — " + okFiles + " ملف، المدة " + (int)dur.TotalSeconds + " ثانية." + (warns > 0 ? "  (تنبيهات: " + warns + ")" : ""));
                    LogOp("تم إنشاء نسخة احتياطية — " + okFiles + " ملف");
                }
                catch (Exception ex) { BkLog("خطأ: " + ex.Message); }
                finally { EndOp(); if (bkBackupBtn.InvokeRequired) bkBackupBtn.BeginInvoke(new Action(() => { bkBackupBtn.Enabled = true; bkRestoreBtn.Enabled = true; })); }
            });
        }

        // يكتشف مجلد النسخة الفعلي: إما المجلد نفسه إذا فيه DCIM مباشرة،
        // أو أحدث مجلد فرعي (مثل CenterPoint_Backup_...) يحتوي النسخة.
        string DetectBackupRoot(string folder)
        {
            bool HasBackup(string p)
            {
                foreach (var b in BK) if (Directory.Exists(Path.Combine(p, b.disk))) return true;
                return File.Exists(Path.Combine(p, "Contacts.vcf"));
            }
            if (HasBackup(folder)) return folder;
            try
            {
                var subs = Directory.GetDirectories(folder);
                Array.Sort(subs); Array.Reverse(subs); // الأحدث أولاً (الاسم فيه التاريخ)
                foreach (var sub in subs) if (HasBackup(sub)) return sub;
            }
            catch { }
            return null;
        }

        void StartRestore()
        {
            if (bkDev.SelectedIndex < 0 || bkDev.SelectedIndex >= devices.Count) { MessageBox.Show("اختر الجهاز."); return; }
            if (bkFolder == "" || !Directory.Exists(bkFolder)) { MessageBox.Show("اختر مجلد النسخة الاحتياطية (الذي يحتوي DCIM وغيرها)."); return; }
            var d = devices[bkDev.SelectedIndex];
            if (d.Kind == "ios") { IosRestore(d); return; }
            // التحقق من النسخة قبل الاستعادة
            string root0 = DetectBackupRoot(bkFolder);
            if (root0 == null) { MessageBox.Show("لم أجد نسخة احتياطية صالحة في هذا المجلد\n(ابحث عن مجلد يحتوي DCIM أو CenterPoint_Backup_...)."); return; }
            int nFiles = 0; long nBytes = 0;
            try { foreach (var f in Directory.GetFiles(root0, "*", SearchOption.AllDirectories)) { nFiles++; try { nBytes += new FileInfo(f).Length; } catch { } } } catch { }
            if (nFiles == 0) { MessageBox.Show("⚠ النسخة الاحتياطية فارغة — لا يوجد ما يُستعاد."); return; }
            if (MessageBox.Show("✔ تم التحقق من النسخة الاحتياطية:\n\n" + nFiles + " ملف   (" + FmtSize(nBytes) + ")\nالمجلد: " + root0 + "\n\nسيتم نسخ المحتوى إلى الهاتف. متابعة؟",
                "تأكيد الاستعادة", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (!BeginOp("النسخ الاحتياطي/الاستعادة")) return;
            bkBackupBtn.Enabled = false; bkRestoreBtn.Enabled = false;
            Task.Run(() =>
            {
                try
                {
                    BkLog(""); BkLog("بدء الاستعادة إلى " + d.Model);
                    string root = DetectBackupRoot(bkFolder);
                    if (root == null)
                    {
                        BkLog("✗ لم أجد نسخة احتياطية في هذا المجلد (ابحث عن مجلد فيه DCIM أو CenterPoint_Backup_...).");
                        BkProg(0);
                        return;
                    }
                    BkLog("المجلد المصدر: " + root);
                    int i = 0;
                    foreach (var b in BK)
                    {
                        i++;
                        BkProg((double)(i - 1) / BK.Length * 100);
                        string local = Path.Combine(root, b.disk);
                        if (!Directory.Exists(local)) continue;
                        string parent = b.dev.Substring(0, b.dev.LastIndexOf('/'));
                        BkLog("── " + b.disk + " ── جاري الاستعادة...");
                        Shell.Run("adb", "-s " + d.Id + " shell mkdir -p " + parent);
                        var pr = Shell.Run("adb", "-s " + d.Id + " push " + Shell.Q(local) + " " + parent, 3600000);
                        BkLog(pr.ok ? "  ✔ تم" : "  ✗ فشل: " + Trim(pr.outp));
                    }
                    string vcf = Path.Combine(root, "Contacts.vcf");
                    if (File.Exists(vcf))
                    {
                        BkLog("── جهات الاتصال ──");
                        Shell.Run("adb", "-s " + d.Id + " push " + Shell.Q(vcf) + " /sdcard/Download/CenterPoint_Contacts.vcf");
                        Shell.Run("adb", "-s " + d.Id + " shell am start -a android.intent.action.VIEW -d file:///sdcard/Download/CenterPoint_Contacts.vcf -t text/x-vcard");
                        BkLog("  ✔ افتح الهاتف واضغط استيراد.");
                    }
                    BkProg(100);
                    BkLog(""); BkLog("✅ انتهت الاستعادة.");
                }
                catch (Exception ex) { BkLog("خطأ: " + ex.Message); }
                finally { EndOp(); if (bkBackupBtn.InvokeRequired) bkBackupBtn.BeginInvoke(new Action(() => { bkBackupBtn.Enabled = true; bkRestoreBtn.Enabled = true; })); }
            });
        }

        // ============ نسخ/استعادة آيفون كامل (آلية آبل الرسمية — backup2) ============
        // للنقل آيفون ← آيفون: (1) انسخ الآيفون المصدر إلى مجلد، (2) بدّل الجهاز للآيفون الهدف واستعد نفس المجلد.
        // لا تجاوز لأي قفل أو حماية: تتطلب فتح الجهاز والثقة بالكمبيوتر وإيقاف Find My عند الاستعادة.
        void IosBackup(Device d)
        {
            if (bkFolder == "" || !Directory.Exists(bkFolder)) { MessageBox.Show("اختر مجلد النسخ أول."); return; }
            if (MessageBox.Show(
                "نسخ احتياطي كامل للآيفون عبر آلية آبل الرسمية.\n\n" +
                "المتطلبات:\n" +
                "• iTunes / Apple Devices وتعريفات آبل مثبتة.\n" +
                "• الآيفون مفتوح القفل و«الوثوق بهذا الكمبيوتر» مفعّل.\n" +
                "• لنقل كلمات المرور والصحة: فعّل «تشفير النسخة الاحتياطية» في iTunes أولاً.\n\n" +
                "قد تستغرق العملية وقتاً حسب حجم البيانات. متابعة؟",
                "نسخ احتياطي آيفون", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;

            if (!BeginOp("النسخ الاحتياطي/الاستعادة")) return;
            bkBackupBtn.Enabled = false; bkRestoreBtn.Enabled = false;
            Task.Run(() =>
            {
                try
                {
                    string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
                    string root = Path.Combine(bkFolder, "CenterPoint_iPhoneBackup_" + stamp);
                    Directory.CreateDirectory(root);
                    BkLog(""); BkLog("بدء النسخ الاحتياطي للآيفون " + d.Id);
                    BkLog("المجلد: " + root);
                    BkLog("جاري النسخ عبر backup2 ... (لا تفصل الكيبل)");
                    BkProg(10);
                    var r = Shell.Run("pymobiledevice3", "backup2 backup --full " + Shell.Q(root) + " --udid " + d.Id, 7200000);
                    BkProg(100);
                    if (r.ok)
                    {
                        BkLog("✅ انتهى النسخ الاحتياطي للآيفون.");
                        BkLog("للنقل آيفون ← آيفون: بدّل «الجهاز» للآيفون الهدف ثم اضغط «استعادة» على نفس المجلد.");
                        LogOp("نسخة احتياطية كاملة لآيفون");
                    }
                    else
                    {
                        BkLog("✗ تعذّر إكمال النسخ: " + Trim(r.outp));
                        BkLog("تأكد من: تثبيت تعريفات آبل، فتح قفل الآيفون، والضغط على «الوثوق».");
                    }
                }
                catch (Exception ex) { BkLog("خطأ: " + ex.Message); }
                finally { EndOp(); if (bkBackupBtn.InvokeRequired) bkBackupBtn.BeginInvoke(new Action(() => { bkBackupBtn.Enabled = true; bkRestoreBtn.Enabled = true; })); }
            });
        }

        void IosRestore(Device d)
        {
            if (bkFolder == "" || !Directory.Exists(bkFolder)) { MessageBox.Show("اختر مجلد النسخة الاحتياطية أول."); return; }
            if (MessageBox.Show(
                "⚠ استعادة نسخة آيفون كاملة إلى هذا الجهاز.\n\n" +
                "تنبيهات مهمة:\n" +
                "• سيتم استبدال بيانات الآيفون الهدف بالكامل بمحتوى النسخة.\n" +
                "• يجب إيقاف «تتبع الموقع/Find My» على الآيفون الهدف قبل الاستعادة.\n" +
                "• الآيفون الهدف يلزم أن يكون بنفس إصدار iOS أو أحدث من المصدر.\n" +
                "• إذا كانت النسخة مشفّرة ستحتاج كلمة مرور النسخة الاحتياطية.\n" +
                "• سيعاد تشغيل الجهاز بعد الانتهاء.\n\n" +
                "هذه هي آلية آبل الرسمية ولا تتجاوز أي حماية. متابعة؟",
                "استعادة آيفون", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            // اكتشاف مجلد النسخة (قد يكون المجلد نفسه أو أحدث مجلد فرعي CenterPoint_iPhoneBackup_)
            string root = bkFolder;
            try
            {
                if (!Directory.Exists(Path.Combine(bkFolder, d.Id)))
                {
                    var subs = Directory.GetDirectories(bkFolder);
                    Array.Sort(subs); Array.Reverse(subs);
                    foreach (var sub in subs)
                        if (Directory.GetDirectories(sub).Any(x => Regex.IsMatch(Path.GetFileName(x), @"^[0-9a-fA-F-]{20,}$")))
                        { root = sub; break; }
                }
            }
            catch { }

            if (!BeginOp("النسخ الاحتياطي/الاستعادة")) return;
            bkBackupBtn.Enabled = false; bkRestoreBtn.Enabled = false;
            Task.Run(() =>
            {
                try
                {
                    BkLog(""); BkLog("بدء الاستعادة إلى الآيفون " + d.Id);
                    BkLog("المجلد المصدر: " + root);
                    BkLog("جاري الاستعادة عبر backup2 ... (لا تفصل الكيبل، وسيعاد التشغيل تلقائياً)");
                    BkProg(10);
                    var r = Shell.Run("pymobiledevice3", "backup2 restore --system --settings --reboot " + Shell.Q(root) + " --udid " + d.Id, 7200000);
                    BkProg(100);
                    if (r.ok)
                    {
                        BkLog("✅ انتهت الاستعادة. انتظر إعادة تشغيل الآيفون وإكمال الإعداد.");
                        LogOp("استعادة نسخة آيفون كاملة");
                    }
                    else
                    {
                        BkLog("✗ تعذّر إكمال الاستعادة: " + Trim(r.outp));
                        BkLog("أكثر سبب شائع: «Find My» ما زال مفعّلاً على الجهاز الهدف — أوقفه ثم أعد المحاولة.");
                        BkLog("أو النسخة مشفّرة وتحتاج كلمة مرور، أو إصدار iOS الهدف أقدم من المصدر.");
                    }
                }
                catch (Exception ex) { BkLog("خطأ: " + ex.Message); }
                finally { EndOp(); if (bkBackupBtn.InvokeRequired) bkBackupBtn.BeginInvoke(new Action(() => { bkBackupBtn.Enabled = true; bkRestoreBtn.Enabled = true; })); }
            });
        }

        // ============ صفحة نقل التطبيقات (APK) ============
        void BuildAppsPage()
        {
            pageApps = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageApps);

            var title = new Label { Text = "نقل التطبيقات (أندرويد ← أندرويد)", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 14), Size = new Size(675, 30), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageApps.Controls.Add(title);
            Tr(title, "نقل التطبيقات (أندرويد ← أندرويد)", "App Transfer (Android → Android)");

            AddLbl(pageApps, "من:", 560, 50, 130);
            appSrc = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(250, 50), Size = new Size(300, 28), Font = new Font("Arial", 10) };
            pageApps.Controls.Add(appSrc);
            appLoad = new Button { Text = "📋 تحميل قائمة التطبيقات", Location = new Point(40, 50), Size = new Size(200, 28), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            appLoad.FlatAppearance.BorderSize = 0; appLoad.Click += (s, e) => LoadApps();
            pageApps.Controls.Add(appLoad);

            AddLbl(pageApps, "إلى:", 560, 86, 130);
            appDst = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(250, 86), Size = new Size(300, 28), Font = new Font("Arial", 10) };
            pageApps.Controls.Add(appDst);
            appMove = new Button { Text = "▶ نقل المحدد", Location = new Point(40, 86), Size = new Size(200, 28), BackColor = GREEN, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            appMove.FlatAppearance.BorderSize = 0; appMove.Click += (s, e) => MoveApps();
            pageApps.Controls.Add(appMove);

            AddLbl(pageApps, "ملاحظة: تنتقل التطبيقات فقط. بيانات التطبيق وتسجيل الدخول لا تنتقل (حماية) - يسجّل المستخدم الدخول من جديد.", 20, 116, 675, Color.FromArgb(201, 162, 39), 9, false);

            // صف البحث وأزرار التحديد
            var selAll = new Button { Text = "✓ تحديد الكل", Location = new Point(40, 142), Size = new Size(110, 26), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9) };
            selAll.FlatAppearance.BorderColor = GREEN; selAll.Click += (s, e) => AppsSelect(true);
            pageApps.Controls.Add(selAll);
            var deAll = new Button { Text = "✗ إلغاء الكل", Location = new Point(155, 142), Size = new Size(110, 26), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9) };
            deAll.FlatAppearance.BorderColor = Color.Gray; deAll.Click += (s, e) => AppsSelect(false);
            pageApps.Controls.Add(deAll);
            var instPc = new Button { Text = "📲 تثبيت APK من الكمبيوتر", Location = new Point(270, 142), Size = new Size(125, 26), BackColor = Color.FromArgb(130, 80, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            instPc.FlatAppearance.BorderSize = 0; instPc.Click += (s, e) => InstallApkFromPc();
            pageApps.Controls.Add(instPc);
            Tr(instPc, "📲 تثبيت APK من الكمبيوتر", "📲 Install APK from PC");
            appSearch = new TextBox { Location = new Point(400, 142), Size = new Size(295, 26), BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.White, Font = new Font("Arial", 10), RightToLeft = RightToLeft.Yes, BorderStyle = BorderStyle.FixedSingle };
            appSearch.TextChanged += (s, e) => FilterApps();
            pageApps.Controls.Add(appSearch);
            AddLbl(pageApps, "🔎 بحث:", 400, 170, 295, Color.FromArgb(120, 140, 180), 8, false);

            appList = new CheckedListBox { Location = new Point(20, 190), Size = new Size(675, 368), BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.White, Font = new Font("Consolas", 10), CheckOnClick = true, BorderStyle = BorderStyle.None };
            appList.ItemCheck += (s, e) =>
            {
                string disp = appList.Items[e.Index].ToString();
                string pkg = dispToPkg.ContainsKey(disp) ? dispToPkg[disp] : disp;  // المعرّف الحقيقي
                if (e.NewValue == CheckState.Checked) appChecked.Add(pkg); else appChecked.Remove(pkg);
            };
            pageApps.Controls.Add(appList);

            appStatus = new Label { Text = "اضغط «تحميل قائمة التطبيقات».", ForeColor = Color.FromArgb(93, 224, 106), BackColor = BG, Location = new Point(20, 564), Size = new Size(675, 22), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9) };
            pageApps.Controls.Add(appStatus);

            appLog = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(93, 224, 106), Location = new Point(20, 588), Size = new Size(675, 54), Font = new Font("Consolas", 9) };
            pageApps.Controls.Add(appLog);
        }

        // اسم ودّي للتطبيقات المعروفة
        static string AppFriendly(string pkg)
        {
            var map = new Dictionary<string, string>
            {
                {"com.whatsapp","WhatsApp"}, {"com.whatsapp.w4b","WhatsApp Business"},
                {"com.facebook.katana","Facebook"}, {"com.facebook.orca","Messenger"},
                {"com.instagram.android","Instagram"}, {"com.zhiliaoapp.musically","TikTok"},
                {"org.telegram.messenger","Telegram"}, {"com.snapchat.android","Snapchat"},
                {"com.google.android.youtube","YouTube"}, {"com.spotify.music","Spotify"},
                {"com.twitter.android","X (Twitter)"}, {"com.viber.voip","Viber"},
                {"com.google.android.gm","Gmail"}, {"com.android.chrome","Chrome"},
                {"com.google.android.apps.maps","Google Maps"}, {"com.imo.android.imoim","imo"},
                {"com.linkedin.android","LinkedIn"}, {"com.pinterest","Pinterest"},
                {"com.netflix.mediaclient","Netflix"}, {"com.discord","Discord"},
            };
            return map.ContainsKey(pkg) ? map[pkg] : "";
        }

        string Disp(string pkg)
        {
            string f = AppFriendly(pkg);
            return f == "" ? pkg : (f + "   ·   " + pkg);
        }

        void FilterApps()
        {
            string q = appSearch.Text.Trim().ToLower();
            appList.Items.Clear();
            dispToPkg.Clear();
            foreach (var p in allPkgs)
            {
                string disp = Disp(p);
                dispToPkg[disp] = p;
                if (q == "" || disp.ToLower().Contains(q))
                {
                    int idx = appList.Items.Add(disp);
                    if (appChecked.Contains(p)) appList.SetItemChecked(idx, true);  // التحديد محفوظ بالمعرّف الحقيقي
                }
            }
            appStatus.Text = "معروض: " + appList.Items.Count + " / " + allPkgs.Count + "   |   المحدد: " + appChecked.Count;
        }

        void AppsSelect(bool sel)
        {
            for (int i = 0; i < appList.Items.Count; i++) appList.SetItemChecked(i, sel);
            appStatus.Text = "معروض: " + appList.Items.Count + " / " + allPkgs.Count + "   |   المحدد: " + appChecked.Count;
        }

        void AppStat(string t) { if (appStatus.InvokeRequired) appStatus.BeginInvoke(new Action(() => appStatus.Text = t)); else appStatus.Text = t; }
        void AppLg(string m) { if (appLog.InvokeRequired) { appLog.BeginInvoke(new Action(() => AppLg(m))); return; } appLog.AppendText(m + "\r\n"); }

        void LoadApps()
        {
            if (appSrc.SelectedIndex < 0 || appSrc.SelectedIndex >= devices.Count) { MessageBox.Show("اختر جهاز المصدر."); return; }
            var d = devices[appSrc.SelectedIndex];
            if (d.Kind != "android") { MessageBox.Show("نقل التطبيقات يدعم أندرويد فقط."); return; }
            appList.Items.Clear(); allPkgs.Clear(); appChecked.Clear(); appLoad.Enabled = false; appLoad.Text = "جاري...";
            AppStat("جاري قراءة التطبيقات...");
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + d.Id + " shell pm list packages -3");
                var pkgs = new List<string>();
                if (r.ok)
                    foreach (var ln in r.outp.Replace("\r", "").Split('\n'))
                    {
                        string p = ln.Trim();
                        if (p.StartsWith("package:")) pkgs.Add(p.Substring(8).Trim());
                    }
                pkgs.Sort();
                void fill() { allPkgs = pkgs; FilterApps(); appLoad.Enabled = true; appLoad.Text = "📋 تحميل قائمة التطبيقات"; }
                if (appList.InvokeRequired) appList.BeginInvoke(new Action(fill)); else fill();
            });
        }

        void MoveApps()
        {
            if (appSrc.SelectedIndex < 0 || appDst.SelectedIndex < 0) { MessageBox.Show("اختر المصدر والوجهة."); return; }
            if (appSrc.SelectedIndex == appDst.SelectedIndex) { MessageBox.Show("لازم جهازين مختلفين."); return; }
            var checkedPkgs = appChecked.ToList();   // معرّفات التطبيقات الحقيقية
            if (checkedPkgs.Count == 0) { MessageBox.Show("أشّر تطبيق واحد على الأقل."); return; }
            var s = devices[appSrc.SelectedIndex]; var d = devices[appDst.SelectedIndex];
            if (d.Kind != "android") { MessageBox.Show("التثبيت يدعم أندرويد فقط."); return; }
            // تأكيد قبل التثبيت + توضيح أن بيانات التطبيق وتسجيل الدخول لا تنتقل
            if (MessageBox.Show(
                "سيتم تثبيت " + checkedPkgs.Count + " تطبيق على:\n" + d.Model + "\n\n" +
                "⚠ تنتقل التطبيقات نفسها فقط. بيانات التطبيق وتسجيل الدخول لا تنتقل — يسجّل المستخدم الدخول من جديد.\n\nمتابعة؟",
                "تأكيد تثبيت التطبيقات", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (!BeginOp("نقل التطبيقات")) return;
            appMove.Enabled = false; appMove.Text = "جاري النقل...";
            string tmp = Path.Combine(Path.GetTempPath(), "cp_apk_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(tmp);
            Task.Run(() =>
            {
              try
              {
                int ok = 0, fail = 0, i = 0;
                foreach (var pkg in checkedPkgs)
                {
                    i++;
                    string disp = Disp(pkg);
                    AppStat("(" + i + "/" + checkedPkgs.Count + ") " + disp);
                    AppLg("── " + disp);
                    var pr = Shell.Run("adb", "-s " + s.Id + " shell pm path " + pkg);
                    if (!pr.ok) { AppLg("  ✗ تعذّر إيجاد الملف"); fail++; continue; }
                    var apks = new List<string>();
                    int k = 0;
                    foreach (var ln in pr.outp.Replace("\r", "").Split('\n'))
                    {
                        string p = ln.Trim();
                        if (p.StartsWith("package:"))
                        {
                            string remote = p.Substring(8).Trim();
                            string local = Path.Combine(tmp, pkg + "_" + (k++) + ".apk");
                            var pl = Shell.Run("adb", "-s " + s.Id + " pull " + Shell.Q(remote) + " " + Shell.Q(local), 600000);
                            if (pl.ok && File.Exists(local)) apks.Add(local);
                        }
                    }
                    if (apks.Count == 0) { AppLg("  ✗ فشل سحب الملف"); fail++; continue; }
                    (bool ok, string outp) ins;
                    if (apks.Count == 1)
                        ins = Shell.Run("adb", "-s " + d.Id + " install -r -d " + Shell.Q(apks[0]), 600000);
                    else
                    { AppLg("  📦 تطبيق مقسّم (Split APK): " + apks.Count + " ملف"); ins = Shell.Run("adb", "-s " + d.Id + " install-multiple -r -d " + string.Join(" ", apks.Select(Shell.Q)), 600000); }
                    if (ins.ok && ins.outp.Contains("Success")) { AppLg("  ✔ تم التثبيت"); ok++; }
                    else { AppLg("  ✗ فشل التثبيت: " + Trim(ins.outp)); fail++; LogError("تثبيت تطبيق", disp + " | " + Trim(ins.outp)); }
                }
                try { Directory.Delete(tmp, true); } catch { }
                AppStat("✅ انتهى: نجح " + ok + "، فشل " + fail + " من " + checkedPkgs.Count);
                AppLg("انتهى. نجح " + ok + "، فشل " + fail + ".");
                if (ok > 0) LogOp("تم تثبيت " + ok + " تطبيق");
              }
              finally
              {
                EndOp();
                if (appMove.InvokeRequired) appMove.BeginInvoke(new Action(() => { appMove.Enabled = true; appMove.Text = "▶ نقل المحدد"; }));
              }
            });
        }

        // تثبيت ملف/ملفات APK من الكمبيوتر إلى الجهاز (بموافقة المستخدم)
        void InstallApkFromPc()
        {
            if (devices.Count == 0) { MessageBox.Show("وصّل جهاز أندرويد أولاً."); return; }
            int idx = appDst.SelectedIndex >= 0 ? appDst.SelectedIndex : (appSrc.SelectedIndex >= 0 ? appSrc.SelectedIndex : 0);
            if (idx < 0 || idx >= devices.Count) { MessageBox.Show("اختر الجهاز الهدف من قائمة «إلى»."); return; }
            var d = devices[idx];
            if (d.Kind != "android") { MessageBox.Show("تثبيت APK مدعوم على أندرويد فقط."); return; }
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "اختر ملف/ملفات APK للتثبيت";
                ofd.Filter = "ملفات APK (*.apk)|*.apk";
                ofd.Multiselect = true;
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                var files = ofd.FileNames;
                if (files.Length == 0) return;
                if (MessageBox.Show("سيتم تثبيت " + files.Length + " ملف APK على:\n" + d.Model + "\n\nقد يطلب الهاتف تأكيد التثبيت. متابعة؟",
                    "تأكيد التثبيت", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                appMove.Enabled = false;
                Task.Run(() =>
                {
                    int ok = 0, fail = 0;
                    foreach (var f in files)
                    {
                        AppLg("── تثبيت: " + Path.GetFileName(f));
                        var ins = Shell.Run("adb", "-s " + d.Id + " install -r -d " + Shell.Q(f), 600000);
                        if (ins.ok && ins.outp.Contains("Success")) { AppLg("  ✔ تم"); ok++; }
                        else { AppLg("  ✗ فشل: " + Trim(ins.outp)); fail++; }
                    }
                    AppStat("✅ تثبيت من الكمبيوتر: نجح " + ok + "، فشل " + fail);
                    if (ok > 0) LogOp("تثبيت " + ok + " APK من الكمبيوتر");
                    if (appMove.InvokeRequired) appMove.BeginInvoke(new Action(() => appMove.Enabled = true));
                });
            }
        }

        // ============ صفحة الحسابات ============
        static string AccFriendly(string type)
        {
            string t = type.ToLower();
            if (t.Contains("google")) return "Google";
            if (t.Contains("facebook")) return "Facebook";
            if (t.Contains("instagram")) return "Instagram";
            if (t.Contains("whatsapp")) return "WhatsApp";
            if (t.Contains("twitter") || t == "com.twitter.android") return "X (Twitter)";
            if (t.Contains("telegram")) return "Telegram";
            if (t.Contains("snapchat")) return "Snapchat";
            if (t.Contains("microsoft") || t.Contains("outlook")) return "Microsoft";
            if (t.Contains("yahoo")) return "Yahoo";
            if (t.Contains("apple")) return "Apple ID";
            return type;
        }

        void BuildAccountsPage()
        {
            pageAccounts = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageAccounts);

            var title = new Label { Text = "الحسابات على الجهاز", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 14), Size = new Size(675, 30), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageAccounts.Controls.Add(title);
            Tr(title, "الحسابات على الجهاز", "Device Accounts");

            AddLbl(pageAccounts, "الجهاز:", 560, 50, 130);
            accDev = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(250, 50), Size = new Size(300, 28), Font = new Font("Arial", 10) };
            pageAccounts.Controls.Add(accDev);
            accLoad = new Button { Text = "🔍 عرض الحسابات", Location = new Point(40, 50), Size = new Size(200, 28), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            accLoad.FlatAppearance.BorderSize = 0; accLoad.Click += (s, e) => LoadAccounts();
            pageAccounts.Controls.Add(accLoad);

            var openSet = new Button { Text = "⚙ فتح إعدادات الحسابات على الجهاز (لإزالتها بموافقة المالك)", Location = new Point(20, 312), Size = new Size(675, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            openSet.FlatAppearance.BorderColor = BLUE; openSet.Click += (s, e) => OpenAccountSettings();
            pageAccounts.Controls.Add(openSet);

            accStatus = new Label { Text = "اضغط «عرض الحسابات» لرؤية حسابات الزبون على الجهاز.", ForeColor = Color.FromArgb(93, 224, 106), BackColor = BG, Location = new Point(20, 86), Size = new Size(675, 24), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9) };
            pageAccounts.Controls.Add(accStatus);

            accList = new ListBox { Location = new Point(20, 116), Size = new Size(675, 188), BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.White, Font = new Font("Consolas", 11), BorderStyle = BorderStyle.None };
            pageAccounts.Controls.Add(accList);

            var gtitle = new Label { Text = "⚠ إرشادات مهمة (لا يمكن نقل كلمات المرور)", ForeColor = Color.FromArgb(201, 162, 39), BackColor = BG, Location = new Point(20, 378), Size = new Size(675, 24), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 11, FontStyle.Bold) };
            pageAccounts.Controls.Add(gtitle);

            var guide = new TextBox
            {
                Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true,
                BackColor = CARD, ForeColor = Color.White, RightToLeft = RightToLeft.Yes,
                Location = new Point(20, 406), Size = new Size(675, 234), Font = new Font("Arial", 10), BorderStyle = BorderStyle.None
            };
            guide.Text =
                "كلمات المرور وتسجيل الدخول لا تُنقل بين الأجهزة (حماية). على الجهاز الجديد سجّل الدخول يدوياً:\r\n\r\n" +
                "• Google: الإعدادات ← الحسابات ← إضافة حساب ← Google. فعّل المصادقة الثنائية وتأكد من رقم الاسترداد والإيميل البديل.\r\n\r\n" +
                "• Apple ID: الإعدادات ← تسجيل الدخول. للاستعادة استخدم appleid.apple.com. تأكد من رقم الثقة ومفتاح الاسترداد إن وُجد.\r\n\r\n" +
                "• Facebook / Instagram: افتح التطبيق وسجّل الدخول. استخدم الاستعادة عبر الإيميل أو رقم الهاتف، وفعّل المصادقة الثنائية.\r\n\r\n" +
                "• WhatsApp: يحتاج رقم الهاتف + رمز تحقق SMS. فعّل النسخ الاحتياطي للمحادثات قبل النقل.\r\n\r\n" +
                "نصيحة أمان: قبل تسليم الجهاز القديم، تأكد من تسجيل الخروج من كل الحسابات وإزالتها، وعمل Factory Reset إذا لزم.";
            pageAccounts.Controls.Add(guide);
        }

        void OpenAccountSettings()
        {
            if (accDev.SelectedIndex < 0 || accDev.SelectedIndex >= devices.Count) { MessageBox.Show("اختر الجهاز."); return; }
            var d = devices[accDev.SelectedIndex];
            if (d.Kind != "android") { MessageBox.Show("يدعم أندرويد. على الآيفون: الإعدادات ← اسم الحساب لإزالته."); return; }
            var r = Shell.Run("adb", "-s " + d.Id + " shell am start -a android.settings.SYNC_SETTINGS");
            if (!r.ok) Shell.Run("adb", "-s " + d.Id + " shell am start -a android.settings.SETTINGS");
            MessageBox.Show("فُتحت الإعدادات على شاشة الجهاز.\nبموافقة المالك، أزِل الحسابات من هناك يدوياً.", "إعدادات الجهاز");
        }

        void LoadAccounts()
        {
            if (accDev.SelectedIndex < 0 || accDev.SelectedIndex >= devices.Count) { MessageBox.Show("اختر الجهاز."); return; }
            var d = devices[accDev.SelectedIndex];
            if (d.Kind != "android") { MessageBox.Show("عرض الحسابات يدعم أندرويد بهذي النسخة."); return; }
            accList.Items.Clear(); accLoad.Enabled = false;
            if (accStatus.InvokeRequired) accStatus.BeginInvoke(new Action(() => accStatus.Text = "جاري القراءة...")); else accStatus.Text = "جاري القراءة...";
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + d.Id + " shell dumpsys account");
                var accounts = new List<string>();
                if (r.ok)
                    foreach (Match m in Regex.Matches(r.outp, @"Account\s*\{name=(.*?),\s*type=(.*?)\}"))
                    {
                        string name = m.Groups[1].Value.Trim();
                        string type = AccFriendly(m.Groups[2].Value.Trim());
                        string line = type + "   —   " + name;
                        if (!accounts.Contains(line)) accounts.Add(line);
                    }
                accounts.Sort();
                void fill() { foreach (var a in accounts) accList.Items.Add(a); accLoad.Enabled = true; accStatus.Text = accounts.Count + " حساب على الجهاز. (كلمات المرور غير متاحة - راجع الإرشادات أدناه)"; if (accounts.Count == 0) accStatus.Text = "لم يتم العثور على حسابات (أو يحتاج صلاحية إضافية)."; }
                if (accList.InvokeRequired) accList.BeginInvoke(new Action(fill)); else fill();
            });
        }

        // ============ الصفحة الرئيسية ============
        void BuildHomePage()
        {
            webHome = new WebView2 { Location = new Point(0, 0), Size = new Size(860, 710) };
            Controls.Add(webHome);
            webFallback = new Label { Visible = false, ForeColor = Color.White, BackColor = BG, Location = new Point(0, 0), Size = new Size(860, 710), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 12, FontStyle.Bold) };
            Controls.Add(webFallback);
            InitWeb();
        }

        async void InitWeb()
        {
            try
            {
                string udf = Path.Combine(Path.GetTempPath(), "CenterPoint_WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, udf, null);
                await webHome.EnsureCoreWebView2Async(env);
                webHome.DefaultBackgroundColor = BG;
                webHome.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webHome.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webHome.CoreWebView2.WebMessageReceived += OnWebMsg;
                string html = Path.Combine(AppDir, "ui", "index.html");
                if (File.Exists(html)) webHome.CoreWebView2.Navigate(new Uri(html).AbsoluteUri);
                else webHome.CoreWebView2.NavigateToString("<body style='background:#0f1424;color:#fff;font-family:Arial;text-align:center;padding-top:60px' dir='rtl'><h2>ملف الواجهة ui/index.html غير موجود بجانب البرنامج.</h2></body>");
                webReady = true;
            }
            catch (Exception ex)
            {
                webReady = false;
                if (webHome != null) webHome.Visible = false;
                webFallback.Text = "تعذّر تشغيل الواجهة الاحترافية (WebView2).\n\nثبّت «WebView2 Runtime» من موقع مايكروسوفت ثم أعد تشغيل البرنامج.\n\n" + ex.Message;
                webFallback.Visible = (CurrentPage == "home");
            }
        }

        void OnWebMsg(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string raw;
            try { raw = e.TryGetWebMessageAsString(); } catch { try { raw = e.WebMessageAsJson; } catch { return; } }
            if (string.IsNullOrEmpty(raw)) return;
            string cmd = "", page = "", kind = "";
            int src = -1, dst = -1;
            var catList = new List<string>();
            try
            {
                using (var doc = JsonDocument.Parse(raw))
                {
                    var r = doc.RootElement;
                    if (r.TryGetProperty("cmd", out var c)) cmd = c.GetString() ?? "";
                    if (r.TryGetProperty("page", out var p)) page = p.GetString() ?? "";
                    if (r.TryGetProperty("kind", out var k)) kind = k.GetString() ?? "";
                    if (r.TryGetProperty("src", out var sv) && sv.ValueKind == JsonValueKind.Number) src = sv.GetInt32();
                    if (r.TryGetProperty("dst", out var dv) && dv.ValueKind == JsonValueKind.Number) dst = dv.GetInt32();
                    if (r.TryGetProperty("cats", out var ca) && ca.ValueKind == JsonValueKind.Array)
                        foreach (var it in ca.EnumerateArray()) { var sv2 = it.GetString(); if (!string.IsNullOrEmpty(sv2)) catList.Add(sv2); }
                }
            }
            catch { return; }
            if (cmd == "ready" || cmd == "refresh") { Refresh2(); RefreshHome(); }
            else if (cmd == "nav") { string pg = page; BeginInvoke(new Action(() => ShowPage(pg))); }
            else if (cmd == "quick") { RunQuick(kind); }
            else if (cmd == "startTransfer") StartTransferWeb(src, dst, catList);
            else if (cmd == "cancelTransfer") xferCancel = true;
            else if (cmd == "openReport") OpenLastReport();
        }

        volatile bool xferCancel;
        string xLastReport;
        int xTotal, xDone; long xBytes; DateTime xStart;

        // قفل عمليات موحّد: يمنع تشغيل عمليتين ثقيلتين على الأجهزة بالتزامن
        readonly object opLock = new object();
        volatile bool opBusy; string opName = "";
        bool BeginOp(string name)
        {
            lock (opLock)
            {
                if (opBusy)
                {
                    string msg = "⚠ هناك عملية جارية بالفعل: " + opName + "\nانتظر انتهاءها أو ألغِها قبل بدء عملية جديدة.";
                    try { BeginInvoke(new Action(() => MessageBox.Show(msg, "عملية قيد التنفيذ", MessageBoxButtons.OK, MessageBoxIcon.Warning))); } catch { }
                    PostWeb(JsonSerializer.Serialize(new { type = "toast", text = "هناك عملية جارية: " + opName }));
                    return false;
                }
                opBusy = true; opName = name; return true;
            }
        }
        void EndOp() { lock (opLock) { opBusy = false; opName = ""; } }

        void OpenLastReport()
        {
            if (!string.IsNullOrEmpty(xLastReport) && File.Exists(xLastReport))
            { try { System.Diagnostics.Process.Start(new ProcessStartInfo(xLastReport) { UseShellExecute = true }); } catch { } }
            else PostWeb(JsonSerializer.Serialize(new { type = "toast", text = "لا يوجد تقرير بعد — ابدأ نقلاً أولاً." }));
        }

        // بدء النقل من واجهة الويب: نضبط عناصر WinForms ثم نشغّل نفس محرّك النقل
        void StartTransferWeb(int src, int dst, List<string> cats)
        {
            if (src < 0 || dst < 0 || src >= devices.Count || dst >= devices.Count || src == dst)
            { PostWeb(JsonSerializer.Serialize(new { type = "toast", text = "اختر جهازين مختلفين." })); return; }
            if (cats == null || cats.Count == 0)
            { PostWeb(JsonSerializer.Serialize(new { type = "toast", text = "اختر نوع بيانات واحد على الأقل." })); return; }
            xferCancel = false;
            BeginInvoke(new Action(() =>
            {
                try
                {
                    cbSrc.SelectedIndex = src; cbDst.SelectedIndex = dst;
                    transferSel.Clear();
                    foreach (var c in cats) transferSel.Add(c);
                    Start();
                }
                catch (Exception ex) { PostWeb(JsonSerializer.Serialize(new { type = "toast", text = "تعذّر البدء: " + ex.Message })); }
            }));
        }

        // بثّ تقدّم النقل لواجهة الويب (نسبة/سرعة/عدد/وقت متبقٍّ)
        void PostXfer(string status)
        {
            double secs = (DateTime.Now - xStart).TotalSeconds;
            double mbps = secs > 0 ? (xBytes / 1048576.0 / secs) : 0;
            int remaining = xTotal - xDone; if (remaining < 0) remaining = 0;
            double fps = secs > 0 ? xDone / secs : 0;
            int etaSec = fps > 0.01 ? (int)(remaining / fps) : 0;
            int pct = xTotal > 0 ? (int)((double)xDone / xTotal * 100) : 0;
            PostWeb(JsonSerializer.Serialize(new { type = "xprog", pct = pct, done = xDone, total = xTotal, remaining = remaining, speed = mbps.ToString("0.0"), eta = etaSec, status = status ?? "" }));
        }

        void PostWeb(string json)
        {
            if (!webReady || webHome == null || webHome.CoreWebView2 == null) return;
            if (webHome.InvokeRequired) webHome.BeginInvoke(new Action(() => { try { webHome.CoreWebView2.PostWebMessageAsString(json); } catch { } }));
            else { try { webHome.CoreWebView2.PostWebMessageAsString(json); } catch { } }
        }

        void RefreshHome()
        {
            RefreshOps();
            Task.Run(() =>
            {
                var take = devices.Take(2).ToList();
                var list = new List<object>();
                foreach (var d in take)
                {
                    if (d.Kind == "android")
                    {
                        string model = Devices.Prop(d, "ro.product.model"); if (model == "") model = d.Model;
                        string ver = Devices.Prop(d, "ro.build.version.release");
                        int bat = Devices.Battery(d);
                        var st = Devices.Storage(d);
                        list.Add(new { kind = "android", model = model, os = "Android " + ver, battery = bat, storeUsed = (int)Math.Round(st.used), storeTotal = (int)Math.Round(st.tot), storePct = st.pct });
                    }
                    else
                    {
                        list.Add(new { kind = "ios", model = d.Model, os = "iOS", battery = -1, storeUsed = 0, storeTotal = 0, storePct = 0 });
                    }
                }
                PostWeb(JsonSerializer.Serialize(new { type = "devices", list = list }));
            });
        }

        void RunQuick(string kind)
        {
            if (kind == "usb" || kind == "fix") { BeginInvoke(new Action(() => { ShowPage("usb"); if (kind == "usb") UsbScan(); })); return; }
            var and = devices.FirstOrDefault(x => x.Kind == "android");
            if (and == null) { PostWeb(JsonSerializer.Serialize(new { type = "toast", text = "وصّل جهاز أندرويد أولاً." })); return; }
            Task.Run(() =>
            {
                if (kind == "clean")
                {
                    Shell.Run("adb", "-s " + and.Id + " shell pm trim-caches 999999999999", 120000);
                    LogOp("تنظيف الملفات المؤقتة");
                    PostWeb(JsonSerializer.Serialize(new { type = "toast", text = "تم تنظيف الكاش المؤقت على الجهاز." }));
                }
                else if (kind == "speed")
                {
                    Shell.Run("adb", "-s " + and.Id + " shell pm trim-caches 999999999999", 120000);
                    Shell.Run("adb", "-s " + and.Id + " shell am kill-all", 60000);
                    LogOp("تسريع الهاتف (تنظيف الكاش + إغلاق الخلفية)");
                    PostWeb(JsonSerializer.Serialize(new { type = "toast", text = "تم تنظيف الكاش وإغلاق تطبيقات الخلفية." }));
                }
            });
        }



        void LogOp(string msg)
        {
            try
            {
                string exports = ExportsDir;
                File.AppendAllText(Path.Combine(exports, "operations.log"), msg + "|" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
            RefreshOps();
        }

        // سجل أخطاء مركزي (للتحليل التلقائي في قسم التقارير)
        void LogError(string where, string msg)
        {
            try
            {
                string exports = ExportsDir;
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " | " + where + " | " + (msg ?? "").Replace("\r", " ").Replace("\n", " ");
                File.AppendAllText(Path.Combine(exports, "errors.log"), line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        void RefreshOps()
        {
            var ops = new List<object>();
            try
            {
                string path = Path.Combine(ExportsDir, "operations.log");
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path, Encoding.UTF8).Where(l => l.Contains("|")).ToList();
                    lines.Reverse();
                    foreach (var l in lines.Take(8)) { var p = l.Split('|'); ops.Add(new { text = p[0], time = p.Length > 1 ? p[1] : "" }); }
                }
            }
            catch { }
            PostWeb(JsonSerializer.Serialize(new { type = "ops", list = ops }));
        }

        // ============ قسم فحص وإصلاح USB (بدون ADB) ============
        // ============ حماية البيانات — تقرير الإصلاح (فحص وتشخيص فقط) ============
        void BuildProtectPage()
        {
            pageProtect = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageProtect);

            var title = new Label { Text = "حماية البيانات — تقرير الإصلاح", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageProtect.Controls.Add(title);
            Tr(title, "حماية البيانات — تقرير الإصلاح", "Data Protection — Repair Report");

            // شريط وضع العمل (تحذير أمان)
            var mode = new Label
            {
                Text = "🛡 وضع العمل: فحص وتشخيص فقط قبل أي إصلاح — البرنامج لا ينفّذ فورمات أو تفليش تلقائياً، وأي عملية تمسّ البيانات تحتاج تأكيداً يدوياً صريحاً.",
                ForeColor = Color.White, BackColor = Color.FromArgb(120, 70, 20), Location = new Point(20, 46), Size = new Size(675, 40),
                TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold), Padding = new Padding(8, 0, 8, 0)
            };
            pageProtect.Controls.Add(mode);
            Tr(mode,
               "🛡 وضع العمل: فحص وتشخيص فقط قبل أي إصلاح — البرنامج لا ينفّذ فورمات أو تفليش تلقائياً، وأي عملية تمسّ البيانات تحتاج تأكيداً يدوياً صريحاً.",
               "🛡 Mode: diagnose only before any repair — no automatic format or flashing; any data-affecting action needs explicit manual confirmation.");

            protScanBtn = new Button { Text = "🔍 بدء الفحص والتشخيص", Location = new Point(20, 94), Size = new Size(330, 34), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            protScanBtn.FlatAppearance.BorderSize = 0; protScanBtn.Click += (s, e) => ProtectScan();
            pageProtect.Controls.Add(protScanBtn);
            Tr(protScanBtn, "🔍 بدء الفحص والتشخيص", "🔍 Start Scan & Diagnose");

            protSaveBtn = new Button { Text = "💾 حفظ تقرير الإصلاح", Location = new Point(360, 94), Size = new Size(335, 34), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            protSaveBtn.FlatAppearance.BorderSize = 0; protSaveBtn.Click += (s, e) => ProtectSave();
            pageProtect.Controls.Add(protSaveBtn);
            Tr(protSaveBtn, "💾 حفظ تقرير الإصلاح", "💾 Save Repair Report");

            protResult = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(120, 190, 255), Location = new Point(20, 140), Size = new Size(675, 500), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            protResult.Text = "اضغط «بدء الفحص والتشخيص» لإنشاء تقرير إصلاح آمن (قراءة فقط) للجهاز المتصل.";
            pageProtect.Controls.Add(protResult);
        }

        void SetProtResult(string t) { if (protResult == null) return; if (protResult.InvokeRequired) protResult.BeginInvoke(new Action(() => protResult.Text = t)); else protResult.Text = t; }

        void ProtectSave()
        {
            string content = (protResult == null || string.IsNullOrWhiteSpace(protResult.Text)) ? "" : protResult.Text;
            if (content.Length < 40) { MessageBox.Show("نفّذ الفحص أولاً قبل الحفظ.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            try
            {
                string path = Path.Combine(ExportsDir, "CenterPoint_RepairReport_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
                File.WriteAllText(path, content, new UTF8Encoding(true));
                LogOp("تم حفظ تقرير الإصلاح");
                MessageBox.Show("تم حفظ تقرير الإصلاح في:\r\n" + path, "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ErrBox("تعذّر الحفظ: " + ex.Message); }
        }

        // فحص وتشخيص (قراءة فقط) — لا يعدّل أي شيء على الجهاز
        void ProtectScan()
        {
            if (protScanBtn != null) protScanBtn.Enabled = false;
            SetProtResult("جاري الفحص والتشخيص (قراءة فقط)...");
            Task.Run(() =>
            {
                var sb = new StringBuilder();
                var warns = new List<string>();
                var recs = new List<string>();
                sb.AppendLine("══════════ تقرير الإصلاح — CENTER POINT ══════════");
                sb.AppendLine("التاريخ: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("وضع العمل: فحص وتشخيص فقط — بدون أي تعديل على الجهاز.");
                sb.AppendLine("");

                var d = devices.FirstOrDefault(x => x.Kind == "android");
                if (d == null)
                {
                    var mtp = devices.FirstOrDefault(x => x.Kind == "mtp");
                    var ios = devices.FirstOrDefault(x => x.Kind == "ios");
                    if (mtp != null) sb.AppendLine("● الجهاز المتصل: " + mtp.Model + " (MTP).\r\n   التشخيص الكامل يحتاج «تصحيح USB» (ADB). عبر MTP الفحص محدود (وسائط فقط).");
                    else if (ios != null) sb.AppendLine("● الجهاز المتصل: " + ios.Model + " (آيفون).\r\n   تشخيص آيفون محدود هنا؛ استخدم أدوات Apple الرسمية.");
                    else sb.AppendLine("✖ لا يوجد جهاز أندرويد متصل بوضع ADB.\r\n   وصّل الهاتف وفعّل «تصحيح USB» ثم أعد الفحص.");
                    sb.AppendLine("");
                    sb.AppendLine("════════════════════════════════════════");
                    sb.AppendLine("سنتر بوينت للاتصالات — تشخيص آمن بدون تعديل.");
                    SetProtResult(sb.ToString());
                    if (protScanBtn != null && !protScanBtn.IsDisposed) protScanBtn.BeginInvoke(new Action(() => protScanBtn.Enabled = true));
                    return;
                }

                // 1) الجهاز
                string model = Devices.Prop(d, "ro.product.model"); if (string.IsNullOrWhiteSpace(model)) model = d.Model;
                string maker = Devices.Prop(d, "ro.product.manufacturer");
                string rel = Devices.Prop(d, "ro.build.version.release");
                string sdk = Devices.Prop(d, "ro.build.version.sdk");
                string patch = Devices.Prop(d, "ro.build.version.security_patch");
                sb.AppendLine("● الجهاز:");
                sb.AppendLine("   الموديل: " + maker + " " + model);
                sb.AppendLine("   أندرويد: " + rel + " (SDK " + sdk + ")");
                sb.AppendLine("   ترقيع الأمان: " + (string.IsNullOrWhiteSpace(patch) ? "غير معروف" : patch));
                if (!string.IsNullOrWhiteSpace(patch))
                {
                    DateTime pd;
                    if (DateTime.TryParse(patch, out pd) && (DateTime.Now - pd).TotalDays > 400)
                    { warns.Add("ترقيع الأمان قديم (" + patch + ")."); recs.Add("انصح الزبون بتحديث النظام إن توفّر."); }
                }
                sb.AppendLine("");

                // 2) التخزين
                var st = Devices.Storage(d);
                sb.AppendLine("● التخزين:");
                if (st.tot > 0)
                {
                    sb.AppendLine("   الكلي: " + st.tot.ToString("0.0") + " GB — متاح: " + st.free.ToString("0.0") + " GB (ممتلئ " + st.pct + "%)");
                    if (st.pct >= 92) { warns.Add("التخزين ممتلئ (" + st.pct + "%)."); recs.Add("فرّغ مساحة أو انقل الوسائط قبل أي صيانة."); }
                }
                else sb.AppendLine("   غير متاح القراءة.");
                sb.AppendLine("");

                // 3) البطارية
                sb.AppendLine("● البطارية:");
                var bat = Shell.Run("adb", "-s " + d.Id + " shell dumpsys battery", 20000);
                string blevel = Rx(bat.outp, @"level:\s*(\d+)"), bhealth = Rx(bat.outp, @"health:\s*(\d+)"), btemp = Rx(bat.outp, @"temperature:\s*(\d+)");
                sb.AppendLine("   الشحن: " + (blevel == "" ? "?" : blevel + "%"));
                string hTxt = bhealth == "2" ? "جيدة" : bhealth == "3" ? "زائدة الحرارة" : bhealth == "4" ? "تالفة" : bhealth == "5" ? "فولتية زائدة" : bhealth == "7" ? "باردة" : "غير معروفة";
                sb.AppendLine("   الصحة: " + hTxt);
                if (btemp != "" && int.TryParse(btemp, out int tval)) sb.AppendLine("   الحرارة: " + (tval / 10.0).ToString("0.0") + "°C");
                if (bhealth == "3" || bhealth == "4" || bhealth == "5") { warns.Add("حالة البطارية: " + hTxt + "."); recs.Add("افحص البطارية قبل أي عملية طويلة."); }
                sb.AppendLine("");

                // 4) التشفير وحالة الحماية (قراءة فقط — معلوماتي)
                sb.AppendLine("● حالة الحماية (معلوماتي فقط — بدون أي تجاوز):");
                string crypto = Devices.Prop(d, "ro.crypto.state");
                sb.AppendLine("   التشفير: " + (string.IsNullOrWhiteSpace(crypto) ? "غير معروف" : (crypto == "encrypted" ? "مشفّر ✔" : crypto)));
                string locked = Devices.Prop(d, "ro.boot.flash.locked");
                string verity = Devices.Prop(d, "ro.boot.veritymode");
                sb.AppendLine("   قفل البوت لودر: " + (locked == "1" ? "مقفل (آمن) ✔" : locked == "0" ? "مفتوح ⚠" : "غير معروف"));
                string su = Shell.Run("adb", "-s " + d.Id + " shell which su", 15000).outp;
                bool rooted = !string.IsNullOrWhiteSpace(su) && su.Contains("/su");
                sb.AppendLine("   الروت: " + (rooted ? "مروّت ⚠" : "غير مروّت ✔"));
                // FRP / حسابات — عدد فقط، معلوماتي، بدون أي تجاوز
                var accs = Shell.Run("adb", "-s " + d.Id + " shell dumpsys account", 20000).outp;
                int gcount = 0; foreach (Match m in Regex.Matches(accs ?? "", @"com\.google")) gcount++;
                sb.AppendLine("   حساب Google مرتبط: " + (gcount > 0 ? "نعم (قفل FRP فعّال عند إعادة الضبط) ⚠" : "لا يبدو مرتبطاً"));
                if (gcount > 0) recs.Add("قبل أي إعادة ضبط: سجّل خروج حساب Google أو تأكّد أن الزبون يعرف كلمة مروره (لتفادي قفل FRP). لا نتجاوز القفل.");
                sb.AppendLine("");

                // 5) سجل أخطاء البرنامج
                try
                {
                    string el = Path.Combine(ExportsDir, "errors.log");
                    int ec = File.Exists(el) ? File.ReadAllLines(el).Count(x => x.Trim() != "") : 0;
                    sb.AppendLine("● سجل أخطاء البرنامج: " + ec + " إدخال.");
                    sb.AppendLine("");
                }
                catch { }

                // 6) التحذيرات والتوصيات
                sb.AppendLine("──────── الملاحظات ────────");
                if (warns.Count == 0) sb.AppendLine("✔ لا توجد مخاطر حرجة ظاهرة.");
                else foreach (var w in warns) sb.AppendLine("⚠ " + w);
                sb.AppendLine("");
                sb.AppendLine("──────── التوصيات (إرشاد فقط) ────────");
                recs.Insert(0, "خذ نسخة احتياطية (قسم «نسخ احتياطي») قبل أي صيانة.");
                int ri = 1; foreach (var r in recs.Distinct()) sb.AppendLine("   " + (ri++) + ") " + r);
                sb.AppendLine("");
                sb.AppendLine("⛔ تنبيه حماية البيانات:");
                sb.AppendLine("   هذا التقرير تشخيصي فقط. البرنامج لا ينفّذ فورمات أو تفليش أو مسح تلقائياً.");
                sb.AppendLine("   أي عملية تمسّ البيانات تتطلب تأكيداً يدوياً صريحاً من الفنّي، وبعد أخذ نسخة احتياطية.");
                sb.AppendLine("════════════════════════════════════════");
                sb.AppendLine("سنتر بوينت للاتصالات — تشخيص آمن بدون تعديل.");

                SetProtResult(sb.ToString());
                if (protScanBtn != null && !protScanBtn.IsDisposed) protScanBtn.BeginInvoke(new Action(() => protScanBtn.Enabled = true));
            });
        }

        static string Rx(string s, string pat)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var m = Regex.Match(s, pat); return m.Success ? m.Groups[1].Value : "";
        }

        // ============ مركز الإصلاح (Smart Recovery + Firmware Manager + Firmware Flashing) ============
        void BuildRepairPage()
        {
            pageRepair = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageRepair);

            var title = new Label { Text = "🔧 مركز الإصلاح", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageRepair.Controls.Add(title);
            Tr(title, "🔧 مركز الإصلاح", "🔧 Repair Center");

            var warn = new Label
            {
                Text = "⛔ البرنامج لا ينفّذ أي تفليش أو فورمات أو مسح تلقائياً. كل عملية تحتاج تأكيداً يدوياً صريحاً + نسخة احتياطية. لا تجاوز لأي قفل حساب (FRP).",
                ForeColor = Color.White, BackColor = Color.FromArgb(140, 35, 35), Location = new Point(20, 46), Size = new Size(675, 40),
                TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold), Padding = new Padding(8, 0, 8, 0)
            };
            pageRepair.Controls.Add(warn);
            Tr(warn,
               "⛔ البرنامج لا ينفّذ أي تفليش أو فورمات أو مسح تلقائياً. كل عملية تحتاج تأكيداً يدوياً صريحاً + نسخة احتياطية. لا تجاوز لأي قفل حساب (FRP).",
               "⛔ No automatic flashing/format/wipe. Every action needs explicit manual confirmation + backup. No account-lock (FRP) bypass.");

            // تبويبات (أربعة)
            repBtnRec = new Button { Text = "🩺 Smart Recovery", Location = new Point(20, 92), Size = new Size(166, 30), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            repBtnRec.FlatAppearance.BorderSize = 0; repBtnRec.Click += (s, e) => RepTab("rec");
            repBtnMgr = new Button { Text = "📦 Firmware Mgr", Location = new Point(190, 92), Size = new Size(166, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            repBtnMgr.FlatAppearance.BorderSize = 0; repBtnMgr.Click += (s, e) => RepTab("mgr");
            repBtnFl = new Button { Text = "⚡ Flashing", Location = new Point(360, 92), Size = new Size(166, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            repBtnFl.FlatAppearance.BorderSize = 0; repBtnFl.Click += (s, e) => RepTab("fl");
            repBtnUnlock = new Button { Text = "🔓 الفتح الشرعي", Location = new Point(530, 92), Size = new Size(165, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            repBtnUnlock.FlatAppearance.BorderSize = 0; repBtnUnlock.Click += (s, e) => RepTab("unlock");
            pageRepair.Controls.Add(repBtnRec); pageRepair.Controls.Add(repBtnMgr); pageRepair.Controls.Add(repBtnFl); pageRepair.Controls.Add(repBtnUnlock);

            // ===== تبويب Smart Recovery =====
            repTabRec = new Panel { Location = new Point(0, 130), Size = new Size(715, 528), BackColor = BG };
            pageRepair.Controls.Add(repTabRec);
            repDetectBtn = new Button { Text = "🔍 كشف حالة الجهاز", Location = new Point(20, 4), Size = new Size(200, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            repDetectBtn.FlatAppearance.BorderSize = 0; repDetectBtn.Click += (s, e) => RepairDetect();
            repRebootBtn = new Button { Text = "🔄 إعادة عادية", Location = new Point(228, 4), Size = new Size(150, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            repRebootBtn.FlatAppearance.BorderColor = BLUE; repRebootBtn.Click += (s, e) => RepairReboot("", "إعادة تشغيل عادية");
            repRecBtn = new Button { Text = "🔧 Recovery", Location = new Point(386, 4), Size = new Size(150, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            repRecBtn.FlatAppearance.BorderColor = BLUE; repRecBtn.Click += (s, e) => RepairReboot("recovery", "الدخول إلى Recovery");
            repFbBtn = new Button { Text = "⚡ Bootloader", Location = new Point(544, 4), Size = new Size(151, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            repFbBtn.FlatAppearance.BorderColor = BLUE; repFbBtn.Click += (s, e) => RepairReboot("bootloader", "الدخول إلى Bootloader/Fastboot");
            repTabRec.Controls.Add(repDetectBtn); repTabRec.Controls.Add(repRebootBtn); repTabRec.Controls.Add(repRecBtn); repTabRec.Controls.Add(repFbBtn);
            repairResult = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(120, 190, 255), Location = new Point(20, 44), Size = new Size(675, 480), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            repairResult.Text = "اضغط «كشف حالة الجهاز» لتشخيص الإقلاع و Recovery و CrashDump. أزرار الإعادة آمنة (لا تمسح بيانات).";
            repTabRec.Controls.Add(repairResult);

            // ===== تبويب Firmware Manager =====
            repTabMgr = new Panel { Location = new Point(0, 130), Size = new Size(715, 528), BackColor = BG, Visible = false };
            pageRepair.Controls.Add(repTabMgr);
            var mgrPick = new Button { Text = "📂 اختيار ملف فيرموير", Location = new Point(20, 4), Size = new Size(230, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            mgrPick.FlatAppearance.BorderSize = 0; mgrPick.Click += (s, e) => RepMgrPick();
            repTabMgr.Controls.Add(mgrPick);
            // البحث عن ملف التفليش
            repMgrModel = new TextBox { Location = new Point(260, 5), Size = new Size(200, 30), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Consolas", 10) };
            repMgrModel.Text = "";
            repTabMgr.Controls.Add(repMgrModel);
            var mgrSearch = new Button { Text = "🔎 بحث عن ملف التفليش", Location = new Point(470, 4), Size = new Size(225, 32), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            mgrSearch.FlatAppearance.BorderSize = 0; mgrSearch.Click += (s, e) => RepSearchFirmware();
            repTabMgr.Controls.Add(mgrSearch);
            repMgrResult = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(120, 190, 255), Location = new Point(20, 44), Size = new Size(675, 480), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            repMgrResult.Text =
                "ℹ ما هو ملف التفليش؟\r\n" +
                "هو حزمة النظام (الروم) الرسمية أو المعدّلة التي تُثبّت على الجهاز بأداة تفليش متوافقة —\r\n" +
                "لإصلاح النظام، أو تحديثه، أو إعادة ضبطه بالكامل. صيغته تتبع المعالج:\r\n" +
                "   • Unisoc/SPD = .pac   • Rockchip = .img   • MediaTek = scatter + صور   • Samsung = .tar/.tar.md5   • OTA = .zip\r\n" +
                "────────────────────────────────────────\r\n" +
                "اختر ملف فيرموير لعرض نوعه وحجمه والتحقق من سلامته (MD5 / SHA-256) ومطابقته للموديل،\r\n" +
                "أو اكتب الموديل واضغط «بحث عن ملف التفليش» لفتح مصادر موثوقة.";
            repTabMgr.Controls.Add(repMgrResult);

            // ===== تبويب Firmware Flashing =====
            repTabFl = new Panel { Location = new Point(0, 130), Size = new Size(715, 528), BackColor = BG, Visible = false };
            pageRepair.Controls.Add(repTabFl);
            var flDetect = new Button { Text = "🔍 كشف وضع الجهاز", Location = new Point(20, 4), Size = new Size(165, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            flDetect.FlatAppearance.BorderSize = 0; flDetect.Click += (s, e) => RepFlDetect();
            var flPick = new Button { Text = "📂 اختيار ملف", Location = new Point(193, 4), Size = new Size(140, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            flPick.FlatAppearance.BorderColor = BLUE; flPick.Click += (s, e) => RepFlPick();
            var flPartLbl = new Label { Text = "القسم:", Location = new Point(341, 10), Size = new Size(50, 20), ForeColor = Color.White, BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9) };
            repFlPartition = new TextBox { Text = "boot", Location = new Point(395, 7), Size = new Size(90, 26), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center, Font = new Font("Consolas", 10) };
            var flFbBtn = new Button { Text = "⚡ fastboot flash", Location = new Point(493, 4), Size = new Size(100, 32), BackColor = Color.FromArgb(150, 90, 20), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            flFbBtn.FlatAppearance.BorderSize = 0; flFbBtn.Click += (s, e) => RepFlFastboot();
            var flSlBtn = new Button { Text = "📥 sideload", Location = new Point(601, 4), Size = new Size(94, 32), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            flSlBtn.FlatAppearance.BorderSize = 0; flSlBtn.Click += (s, e) => RepFlSideload();
            repTabFl.Controls.Add(flDetect); repTabFl.Controls.Add(flPick); repTabFl.Controls.Add(flPartLbl); repTabFl.Controls.Add(repFlPartition); repTabFl.Controls.Add(flFbBtn); repTabFl.Controls.Add(flSlBtn);

            // صف دليل التفليش — كافة الماركات والموديلات (قائمة منسدلة)
            var gLbl = new Label { Text = "الدليل حسب الماركة/المعالج:", Location = new Point(475, 44), Size = new Size(220, 24), ForeColor = Color.FromArgb(201, 162, 39), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold) };
            var flBrand = new ComboBox { Location = new Point(145, 44), Size = new Size(325, 26), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = CARD, ForeColor = Color.White, Font = new Font("Arial", 9) };
            flBrand.Items.AddRange(new object[] {
                "Samsung — Odin / Thor",
                "Xiaomi / Redmi / POCO — Mi Flash / Fastboot",
                "Oppo / Realme / OnePlus — fastboot / MsmDownloadTool",
                "Vivo / iQOO — fastboot / أداة رسمية",
                "Huawei / Honor — eRecovery / HiSuite",
                "Motorola / Lenovo / Nokia / Pixel — Fastboot",
                "Tecno / Infinix / itel — SP Flash Tool (MTK)",
                "LG — LGUP",
                "iPhone / iPad — iTunes / Finder (DFU)",
                "Qualcomm (معالج) — QFIL / QPST",
                "MediaTek (معالج) — SP Flash Tool",
                "Unisoc / SPD (معالج) — ResearchDownload",
                "Rockchip (معالج) — RKDevTool",
                "Allwinner (معالج) — PhoenixSuit / LiveSuit",
            });
            flBrand.SelectedIndex = 0;
            string[] flKeys = { "samsung", "xiaomi", "oppo", "vivo", "huawei", "moto", "tecno", "lg", "iphone", "qcom", "mtk", "spd", "rk", "allwinner" };
            var gShow = new Button { Text = "📖 عرض الدليل", Location = new Point(20, 44), Size = new Size(120, 26), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            gShow.FlatAppearance.BorderColor = BLUE; gShow.Click += (s, e) => { int i = flBrand.SelectedIndex; if (i >= 0 && i < flKeys.Length) FlGuide(flKeys[i]); };
            repTabFl.Controls.Add(gLbl); repTabFl.Controls.Add(flBrand); repTabFl.Controls.Add(gShow);

            // صف البحث عن الروم (ملف التفليش)
            var flSearchBtn = new Button { Text = "🔎 بحث عن الروم", Location = new Point(20, 78), Size = new Size(185, 30), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            flSearchBtn.FlatAppearance.BorderSize = 0; flSearchBtn.Click += (s, e) => FlSearchRom();
            flModel = new TextBox { Location = new Point(215, 79), Size = new Size(480, 28), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Consolas", 10) };
            repTabFl.Controls.Add(flSearchBtn); repTabFl.Controls.Add(flModel);

            repFlResult = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(255, 200, 120), Location = new Point(20, 114), Size = new Size(675, 410), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            repFlResult.Text =
                "التفليش المدعوم مباشرة داخل البرنامج (بأدوات قياسية + تأكيد يدوي):\r\n" +
                "  • fastboot flash <القسم> <ملف .img>  — للأجهزة في وضع Fastboot/Bootloader.\r\n" +
                "  • adb sideload <ملف .zip>  — لتطبيق تحديث OTA من Recovery.\r\n\r\n" +
                "غير مدعوم مباشرة (يحتاج أداة الشركة):\r\n" +
                "  • Unisoc (SPD) مثل CrashDump/TigerDump → أداة ResearchDownload / SPD Upgrade.\r\n" +
                "  • MediaTek → SP Flash Tool.   • Qualcomm → QFIL / EDL.\r\n\r\n" +
                "⛔ أي تفليش قد يمسح البيانات. خذ نسخة احتياطية أولاً، وتأكد أن الفيرموير مطابق للموديل بالضبط.";
            repTabFl.Controls.Add(repFlResult);

            // ===== تبويب الفتح الشرعي =====
            repTabUnlock = new Panel { Location = new Point(0, 130), Size = new Size(715, 528), BackColor = BG, Visible = false };
            pageRepair.Controls.Add(repTabUnlock);
            var ulBoot = new Button { Text = "🔓 فتح البوت لودر (Fastboot)", Location = new Point(20, 4), Size = new Size(260, 32), BackColor = Color.FromArgb(150, 90, 20), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            ulBoot.FlatAppearance.BorderSize = 0; ulBoot.Click += (s, e) => RepUnlockBootloader();
            var ulNet = new Button { Text = "📡 إرشاد فك الشبكة (IMEI)", Location = new Point(288, 4), Size = new Size(240, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            ulNet.FlatAppearance.BorderColor = BLUE; ulNet.Click += (s, e) => RepNetworkGuide();
            repTabUnlock.Controls.Add(ulBoot); repTabUnlock.Controls.Add(ulNet);
            repUnlockResult = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(255, 200, 120), Location = new Point(20, 44), Size = new Size(675, 480), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            repUnlockResult.Text =
                "🔓 الفتح الشرعي فقط — لا تجاوز لقفل حساب Google (FRP) ولا قفل الشاشة.\r\n\r\n" +
                "• فتح البوت لودر: يبدأه المالك (فعّل «OEM Unlocking» من خيارات المطوّر)، ويُنفَّذ بأمر fastboot، ويمسح كل البيانات. غرضه الصيانة والتفليش.\r\n" +
                "• فك الشبكة: رسمي عبر مزوّد الخدمة باستخدام رقم IMEI. البرنامج يعرض الـIMEI ويرشدك، ولا يكسر القفل.";
            repTabUnlock.Controls.Add(repUnlockResult);
        }

        void RepTab(string w)
        {
            if (repTabRec != null) repTabRec.Visible = w == "rec";
            if (repTabMgr != null) repTabMgr.Visible = w == "mgr";
            if (repTabFl != null) repTabFl.Visible = w == "fl";
            if (repTabUnlock != null) repTabUnlock.Visible = w == "unlock";
            if (repBtnRec != null) repBtnRec.BackColor = w == "rec" ? BLUE : CARD;
            if (repBtnMgr != null) repBtnMgr.BackColor = w == "mgr" ? BLUE : CARD;
            if (repBtnFl != null) repBtnFl.BackColor = w == "fl" ? BLUE : CARD;
            if (repBtnUnlock != null) repBtnUnlock.BackColor = w == "unlock" ? BLUE : CARD;
        }

        // فتح البوت لودر (شرعي، يبدأه المالك، يمسح البيانات) — بتأكيد مزدوج
        void RepUnlockBootloader()
        {
            var fb = Shell.Run(FastbootPath(), "devices", 15000);
            bool fbHas = fb.ok && !string.IsNullOrWhiteSpace(fb.outp) && fb.outp.Trim().Length > 2;
            if (!fbHas)
            {
                SetTb(repUnlockResult, "✖ لا يوجد جهاز في وضع Fastboot.\r\n\r\nللدخول: فعّل «OEM Unlocking» و«تصحيح USB» من خيارات المطوّر، ثم ادخل Bootloader من تبويب Smart Recovery ← Bootloader.");
                return;
            }
            string w1 = "⛔ فتح البوت لودر سيمسح كل بيانات الجهاز نهائياً.\r\n\r\n• يُستخدم للصيانة/التفليش فقط.\r\n• يجب أن يكون صاحب الجهاز موافقاً وفعّل «OEM Unlocking».\r\n• هذا ليس تجاوزاً لأي قفل حساب.\r\n\r\nمتابعة؟";
            if (MessageBox.Show(w1, "تأكيد فتح البوت لودر", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            if (MessageBox.Show("تأكيد نهائي: تنفيذ fastboot flashing unlock؟\r\nبعدها اختر «Unlock» على شاشة الجهاز بأزرار الصوت/الطاقة.", "تأكيد نهائي", MessageBoxButtons.YesNo, MessageBoxIcon.Stop) != DialogResult.Yes) return;
            SetTb(repUnlockResult, "جاري: fastboot flashing unlock ...\r\nأكمل التأكيد على شاشة الجهاز.");
            Task.Run(() =>
            {
                var r = Shell.Run(FastbootPath(), "flashing unlock", 120000);
                if (!r.ok || (r.outp ?? "").ToLower().Contains("unknown command") || (r.outp ?? "").ToLower().Contains("fail"))
                {
                    var r2 = Shell.Run(FastbootPath(), "oem unlock", 120000);
                    SetTb(repUnlockResult, "═══ النتيجة ═══\r\n[flashing unlock]\r\n" + (r.outp ?? "") + "\r\n\r\n[oem unlock]\r\n" + (r2.outp ?? "") + "\r\n\r\nأكمل التأكيد على شاشة الجهاز إن طُلب.");
                }
                else SetTb(repUnlockResult, "═══ نتيجة fastboot flashing unlock ═══\r\n\r\n" + (r.outp ?? "") + "\r\n\r\n✔ أكمل «Unlock» على شاشة الجهاز.");
                LogOp("فتح بوت لودر (شرعي)");
            });
        }

        // إرشاد فك الشبكة الرسمي (عرض IMEI فقط)
        void RepNetworkGuide()
        {
            SetTb(repUnlockResult, "جاري قراءة IMEI...");
            Task.Run(() =>
            {
                var d = devices.FirstOrDefault(x => x.Kind == "android");
                var sb = new StringBuilder();
                sb.AppendLine("═══ فك الشبكة (رسمي) ═══");
                sb.AppendLine("");
                string imei = "";
                if (d != null)
                {
                    var r = Shell.Run("adb", "-s " + d.Id + " shell service call iphonesubinfo 1", 15000);
                    imei = ParseImei(r.outp);
                    if (imei == "") { var r2 = Shell.Run("adb", "-s " + d.Id + " shell dumpsys iphonesubinfo", 15000); imei = Rx(r2.outp ?? "", @"Device ID\s*=\s*(\d{14,16})"); }
                }
                sb.AppendLine("IMEI: " + (imei == "" ? "غير متاح عبر ADB — اطلبه بطلب *#06# على الجهاز" : imei));
                sb.AppendLine("");
                sb.AppendLine("الطريقة الرسمية لفك الشبكة:");
                sb.AppendLine("1) احصل على IMEI (من فوق أو بطلب *#06#).");
                sb.AppendLine("2) قدّم طلب فك للشبكة من مزوّد الخدمة المالك للقفل (أو خدمة فك رسمية بالـIMEI).");
                sb.AppendLine("3) عند الموافقة، أدخل رمز الفتح (Network Unlock PIN) عند طلبه بعد وضع شريحة مزوّد آخر.");
                sb.AppendLine("");
                sb.AppendLine("ملاحظة: هذا فك شبكة رسمي بالـIMEI — ليس تجاوزاً لحساب أو قفل شاشة.");
                SetTb(repUnlockResult, sb.ToString());
            });
        }

        // استخراج IMEI من مخرجات service call iphonesubinfo (parcel hex → أرقام)
        static string ParseImei(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var digits = new StringBuilder();
            foreach (Match m in Regex.Matches(s, @"'([^']*)'"))
                foreach (char c in m.Groups[1].Value)
                    if (c >= '0' && c <= '9') digits.Append(c);
            string d = digits.ToString();
            return (d.Length >= 14 && d.Length <= 16) ? d : "";
        }

        void SetTb(TextBox tb, string t) { if (tb == null) return; if (tb.InvokeRequired) tb.BeginInvoke(new Action(() => tb.Text = t)); else tb.Text = t; }

        // مسار fastboot (بجانب adb عادةً)
        string FastbootPath()
        {
            try
            {
                string adb = Shell.AdbExe();
                if (!string.IsNullOrEmpty(adb) && adb.ToLower().EndsWith("adb.exe"))
                {
                    string fb = adb.Substring(0, adb.Length - 7) + "fastboot.exe";
                    if (File.Exists(fb)) return fb;
                }
            }
            catch { }
            return "fastboot";
        }

        // Smart Recovery — كشف الحالة (قراءة فقط)
        void RepairDetect()
        {
            SetTb(repairResult, "جاري كشف حالة الجهاز...");
            Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("═══ كشف حالة الجهاز ═══");
                sb.AppendLine("");
                var ad = Shell.Run("adb", "devices", 20000);
                string adbState = "";
                int adbCount = 0;
                foreach (var ln in (ad.outp ?? "").Replace("\r", "").Split('\n'))
                {
                    var t = ln.Trim(); if (t == "" || t.StartsWith("List of")) continue;
                    var p = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 2) continue; adbCount++; adbState = p[1];
                }
                var fb = Shell.Run(FastbootPath(), "devices", 15000);
                bool fbHas = fb.ok && !string.IsNullOrWhiteSpace(fb.outp) && fb.outp.Trim().Length > 2;

                sb.AppendLine("● النتيجة:");
                if (adbCount > 0 && adbState == "device")
                    sb.AppendLine("   ✔ الجهاز مُقلع ويعمل (ADB متصل). لا توجد مشكلة إقلاع ظاهرة.");
                else if (adbState == "recovery" || adbState == "sideload")
                    sb.AppendLine("   🔧 الجهاز في وضع Recovery/Sideload — يمكن تطبيق تحديث .zip عبر Firmware Flashing.");
                else if (adbState == "unauthorized")
                    sb.AppendLine("   ⚠ الجهاز متصل لكن غير مصرّح — اقبل «السماح بتصحيح USB» على الشاشة.");
                else if (fbHas)
                    sb.AppendLine("   ⚡ الجهاز في وضع Fastboot/Bootloader — يمكن fastboot flash عبر Firmware Flashing.");
                else
                {
                    sb.AppendLine("   ✖ لا يوجد جهاز عبر ADB أو Fastboot.");
                    sb.AppendLine("   إذا كانت شاشة الجهاز تعرض:");
                    sb.AppendLine("     • «CrashDump / TigerDump» أو «Download» → شريحة Unisoc (SPD) غالباً. يُصلَّح بتفليش الفيرموير بأداة SPD، والجهاز لا يظهر بـADB.");
                    sb.AppendLine("     • شعار عالق / Bootloop → جرّب الدخول إلى Recovery يدوياً (أزرار الطاقة + رفع الصوت) ثم «Wipe cache» فقط أولاً.");
                    sb.AppendLine("     • «No command» → اضغط الطاقة + رفع الصوت لإظهار قائمة Recovery.");
                }
                sb.AppendLine("");
                sb.AppendLine("──── دليل الإقلاع السريع ────");
                sb.AppendLine("1) Bootloop بعد تحديث/تطبيق: ادخل Recovery ← «Wipe cache partition» (لا يمسح بيانات) ← Reboot.");
                sb.AppendLine("2) ما زال عالقاً: النسخة الاحتياطية أولاً إن أمكن، ثم فكّر بتفليش فيرموير رسمي مطابق.");
                sb.AppendLine("3) ⚠ «Wipe data/factory reset» في Recovery = يمسح كل بيانات المستخدم — لا تستخدمه إلا بموافقة صاحب الجهاز.");
                sb.AppendLine("4) لا تفلّش فيرموير غير مطابق للموديل — يسبب بريك. تحقق من التوافق في Firmware Manager.");
                sb.AppendLine("");
                sb.AppendLine("ملاحظة: هذا تشخيص وإرشاد. البرنامج لا يمسح ولا يفلّش تلقائياً.");
                SetTb(repairResult, sb.ToString());
            });
        }

        // إعادة تشغيل آمنة (لا تمسح بيانات) — بتأكيد
        void RepairReboot(string arg, string label)
        {
            var dev = devices.FirstOrDefault(x => x.Kind == "android");
            if (dev == null) { MessageBox.Show("لا يوجد جهاز أندرويد متصل بـADB.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (MessageBox.Show(label + "؟\r\n\r\nهذه عملية آمنة (إعادة تشغيل فقط، لا تمسح أي بيانات).", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            SetTb(repairResult, "جاري تنفيذ: " + label + " ...");
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + dev.Id + " reboot " + arg, 25000);
                SetTb(repairResult, "✔ تم إرسال الأمر: adb reboot " + arg + "\r\n\r\n" + Trim(r.outp));
                LogOp("إصلاح: " + label);
            });
        }

        // Firmware Manager — اختيار وتحقق
        void RepMgrPick()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "اختر ملف فيرموير";
                dlg.Filter = "ملفات الفيرموير|*.pac;*.zip;*.img;*.bin;*.tar;*.md5;*.gz;*.txt|كل الملفات|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                repMgrPath = dlg.FileName;
            }
            SetTb(repMgrResult, "الملف: " + Path.GetFileName(repMgrPath) + "\r\nجاري الفحص وحساب البصمة...");
            Task.Run(() =>
            {
                var sb = new StringBuilder();
                try
                {
                    var fi = new FileInfo(repMgrPath);
                    string ext = fi.Extension.ToLower();
                    string type =
                        ext == ".pac" ? "Unisoc (SPD) — يُفلَّش بأداة ResearchDownload / SPD Upgrade" :
                        ext == ".tar" || fi.Name.ToLower().EndsWith(".tar.md5") ? "Samsung (Odin)" :
                        ext == ".img" ? "صورة قسم Android (fastboot flash)" :
                        ext == ".zip" ? "حزمة تحديث/Recovery (adb sideload أو Apply update)" :
                        ext == ".txt" && fi.Name.ToLower().Contains("scatter") ? "MediaTek scatter (SP Flash Tool)" :
                        ext == ".bin" ? "ملف خام (يعتمد على الأداة)" : "غير معروف";
                    sb.AppendLine("═══ معلومات الفيرموير ═══");
                    sb.AppendLine("الاسم:  " + fi.Name);
                    sb.AppendLine("الحجم:  " + FmtSize(fi.Length));
                    sb.AppendLine("النوع:  " + type);
                    sb.AppendLine("التاريخ: " + fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                    sb.AppendLine("");
                    sb.AppendLine("جاري حساب MD5 و SHA-256 (قد يأخذ وقتاً للملفات الكبيرة)...");
                    SetTb(repMgrResult, sb.ToString());

                    string md5 = HashFile(repMgrPath, "md5");
                    string sha = HashFile(repMgrPath, "sha256");
                    sb.AppendLine("MD5:    " + md5);
                    sb.AppendLine("SHA256: " + sha);
                    sb.AppendLine("");
                    sb.AppendLine("التحقق من السلامة: قارن هذه البصمة بالبصمة المنشورة من مصدر الفيرموير.");
                    sb.AppendLine("التوافق: تأكد أن اسم الملف/الموديل يطابق موديل الجهاز بالضبط قبل التفليش (تفليش غير مطابق = بريك).");
                }
                catch (Exception ex) { sb.AppendLine("تعذّر الفحص: " + ex.Message); }
                SetTb(repMgrResult, sb.ToString());
            });
        }

        static string HashFile(string path, string algo)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var h = algo == "md5" ? (System.Security.Cryptography.HashAlgorithm)System.Security.Cryptography.MD5.Create() : System.Security.Cryptography.SHA256.Create())
                {
                    var b = h.ComputeHash(fs);
                    var sbh = new StringBuilder(); foreach (var x in b) sbh.Append(x.ToString("x2"));
                    return sbh.ToString();
                }
            }
            catch (Exception ex) { return "خطأ: " + ex.Message; }
        }

        // البحث عن ملف التفليش (فتح مصادر موثوقة في المتصفح)
        void RepSearchFirmware()
        {
            string model = (repMgrModel == null ? "" : repMgrModel.Text).Trim();
            if (model == "")
            {
                var d = devices.FirstOrDefault(x => x.Kind == "android");
                if (d != null) { model = Devices.Prop(d, "ro.product.model"); if (repMgrModel != null) repMgrModel.Text = model; }
            }
            if (model == "")
            {
                MessageBox.Show("اكتب موديل الجهاز أولاً (مثل M10_C07)، أو وصّل الجهاز بـADB ليُقرأ تلقائياً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string q = Uri.EscapeDataString(model + " firmware");
            string mq = Uri.EscapeDataString(model);
            string gUrl = "https://www.google.com/search?q=" + Uri.EscapeDataString(model + " firmware pac flash file");
            string nUrl = "https://needrom.com/?s=" + mq;
            string hUrl = "https://www.google.com/search?q=site:hovatek.com+" + q;
            OpenUrl(gUrl);
            OpenUrl(nUrl);
            var sb = new StringBuilder();
            sb.AppendLine("🔎 فتحت البحث عن فيرموير: " + model);
            sb.AppendLine("");
            sb.AppendLine("مصادر موثوقة (افتحها بمتصفح الكمبيوتر):");
            sb.AppendLine("• بحث عام:   " + gUrl);
            sb.AppendLine("• Needrom:   " + nUrl);
            sb.AppendLine("• Hovatek:   " + hUrl);
            sb.AppendLine("");
            sb.AppendLine("⚠ تأكد أن الملف:");
            sb.AppendLine("   - يطابق الموديل بالضبط (" + model + ") ورقم البناء إن أمكن.");
            sb.AppendLine("   - بصيغة تناسب معالجك (Unisoc=.pac · Rockchip=.img · MediaTek=scatter).");
            sb.AppendLine("   - من مصدر نظيف — افحصه بمكافح فيروسات قبل التشغيل.");
            sb.AppendLine("");
            sb.AppendLine("الأضمن: اطلب الملف المطابق من الدعم الفني للشركة.");
            SetTb(repMgrResult, sb.ToString());
            LogOp("بحث عن فيرموير: " + model);
        }

        static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
        }

        // البحث عن الروم من تبويب Firmware Flashing (يكتب النتيجة في repFlResult)
        void FlSearchRom()
        {
            string model = (flModel == null ? "" : flModel.Text).Trim();
            if (model == "")
            {
                var d = devices.FirstOrDefault(x => x.Kind == "android");
                if (d != null) { model = Devices.Prop(d, "ro.product.model"); if (flModel != null) flModel.Text = model; }
            }
            if (model == "")
            {
                MessageBox.Show("اكتب موديل الجهاز أولاً (مثل M10_C07)، أو وصّل الجهاز بـADB ليُقرأ تلقائياً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string q = Uri.EscapeDataString(model + " firmware");
            string gUrl = "https://www.google.com/search?q=" + Uri.EscapeDataString(model + " firmware pac flash file");
            string nUrl = "https://needrom.com/?s=" + Uri.EscapeDataString(model);
            string hUrl = "https://www.google.com/search?q=site:hovatek.com+" + q;
            OpenUrl(gUrl);
            OpenUrl(nUrl);
            var sb = new StringBuilder();
            sb.AppendLine("🔎 فتحت البحث عن الروم: " + model);
            sb.AppendLine("");
            sb.AppendLine("مصادر موثوقة:");
            sb.AppendLine("• بحث عام:   " + gUrl);
            sb.AppendLine("• Needrom:   " + nUrl);
            sb.AppendLine("• Hovatek:   " + hUrl);
            sb.AppendLine("");
            sb.AppendLine("⚠ طابق الموديل (" + model + ") والصيغة حسب المعالج (Unisoc=.pac · Rockchip=.img · MediaTek=scatter)، وافحص الملف من الفيروسات.");
            sb.AppendLine("الأضمن: اطلب الملف المطابق من الدعم الفني للشركة.");
            SetTb(repFlResult, sb.ToString());
            LogOp("بحث عن الروم (Flashing): " + model);
        }

        // نفس البحث لكن من قسم السوفت وير (يكتب النتيجة في softResult)
        void SoftSearchFirmware()
        {
            string model = (softModel == null ? "" : softModel.Text).Trim();
            if (model == "")
            {
                var d = devices.FirstOrDefault(x => x.Kind == "android");
                if (d != null) { model = Devices.Prop(d, "ro.product.model"); if (softModel != null) softModel.Text = model; }
            }
            if (model == "")
            {
                MessageBox.Show("اكتب موديل الجهاز أولاً (مثل M10_C07)، أو وصّل الجهاز بـADB ليُقرأ تلقائياً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string q = Uri.EscapeDataString(model + " firmware");
            string mq = Uri.EscapeDataString(model);
            string gUrl = "https://www.google.com/search?q=" + Uri.EscapeDataString(model + " firmware pac flash file");
            string nUrl = "https://needrom.com/?s=" + mq;
            string hUrl = "https://www.google.com/search?q=site:hovatek.com+" + q;
            OpenUrl(gUrl);
            OpenUrl(nUrl);
            var sb = new StringBuilder();
            sb.AppendLine("🔎 فتحت البحث عن فيرموير: " + model);
            sb.AppendLine("");
            sb.AppendLine("مصادر موثوقة (افتحها بمتصفح الكمبيوتر):");
            sb.AppendLine("• بحث عام:   " + gUrl);
            sb.AppendLine("• Needrom:   " + nUrl);
            sb.AppendLine("• Hovatek:   " + hUrl);
            sb.AppendLine("");
            sb.AppendLine("⚠ تأكد أن الملف يطابق الموديل (" + model + ")، وبالصيغة الصحيحة (Unisoc=.pac · Rockchip=.img · MediaTek=scatter)، وافحصه من الفيروسات.");
            sb.AppendLine("الأضمن: اطلب الملف المطابق من الدعم الفني للشركة.");
            SetTb(softResult, sb.ToString());
            LogOp("بحث عن فيرموير (السوفت وير): " + model);
        }

        // Firmware Flashing
        void RepFlDetect()
        {
            SetTb(repFlResult, "جاري كشف وضع الجهاز...");
            Task.Run(() =>
            {
                var sb = new StringBuilder();
                var ad = Shell.Run("adb", "devices", 20000);
                string adbState = ""; int adbCount = 0;
                foreach (var ln in (ad.outp ?? "").Replace("\r", "").Split('\n'))
                {
                    var t = ln.Trim(); if (t == "" || t.StartsWith("List of")) continue;
                    var p = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 2) continue; adbCount++; adbState = p[1];
                }
                var fb = Shell.Run(FastbootPath(), "devices", 15000);
                bool fbHas = fb.ok && !string.IsNullOrWhiteSpace(fb.outp) && fb.outp.Trim().Length > 2;
                sb.AppendLine("═══ وضع الجهاز ═══");
                if (fbHas) sb.AppendLine("⚡ Fastboot — جاهز لـ fastboot flash. اختر ملف .img وحدّد القسم ثم «fastboot flash».");
                else if (adbState == "recovery" || adbState == "sideload") sb.AppendLine("📥 Recovery/Sideload — جاهز لـ adb sideload لملف .zip.");
                else if (adbState == "device") sb.AppendLine("✔ النظام مُقلع (ADB). للتفليش: ادخل Bootloader من «مركز الإصلاح ← Smart Recovery ← Bootloader».");
                else sb.AppendLine("✖ لا جهاز ADB/Fastboot. إن كان بوضع CrashDump/Download (SPD/MTK) فالتفليش يكون بأداة الشركة خارج البرنامج.");
                sb.AppendLine("");
                sb.AppendLine("الملف المختار: " + (repFlPath == "" ? "— لا شيء —" : Path.GetFileName(repFlPath)));
                SetTb(repFlResult, sb.ToString());
            });
        }

        void RepFlPick()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "اختر ملف للتفليش (.img لـ fastboot أو .zip لـ sideload)";
                dlg.Filter = "صور/حزم|*.img;*.zip|كل الملفات|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                repFlPath = dlg.FileName;
            }
            SetTb(repFlResult, "الملف المختار: " + Path.GetFileName(repFlPath) + "\r\n\r\nلـ .img: حدّد القسم ثم «fastboot flash».\r\nلـ .zip: «sideload» (الجهاز في Recovery).");
        }

        void RepFlFastboot()
        {
            if (repFlPath == "" || !File.Exists(repFlPath)) { MessageBox.Show("اختر ملف .img أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string part = (repFlPartition == null ? "" : repFlPartition.Text).Trim();
            if (part == "") { MessageBox.Show("حدّد اسم القسم (مثل boot أو recovery).", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string msg = "⛔ تحذير: أنت على وشك تفليش القسم «" + part + "» بالملف:\r\n" + Path.GetFileName(repFlPath) +
                         "\r\n\r\n• قد يمسح بيانات هذا القسم.\r\n• تأكد أن الملف والقسم مطابقان لموديل الجهاز.\r\n• خذ نسخة احتياطية أولاً.\r\n\r\nمتابعة التفليش؟";
            if (MessageBox.Show(msg, "تأكيد التفليش (fastboot)", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            if (MessageBox.Show("تأكيد نهائي: تنفيذ fastboot flash " + part + "؟", "تأكيد نهائي", MessageBoxButtons.YesNo, MessageBoxIcon.Stop) != DialogResult.Yes) return;
            SetTb(repFlResult, "جاري: fastboot flash " + part + " ...\r\nلا تفصل الجهاز.");
            Task.Run(() =>
            {
                var r = Shell.Run(FastbootPath(), "flash " + part + " " + Shell.Q(repFlPath), 1800000);
                SetTb(repFlResult, "═══ نتيجة fastboot flash " + part + " ═══\r\n\r\n" + (r.outp ?? "") + "\r\n\r\n" + (r.ok ? "✔ انتهى." : "✗ فشل — راجع المخرجات."));
                LogOp("تفليش fastboot: " + part);
            });
        }

        void RepFlSideload()
        {
            if (repFlPath == "" || !File.Exists(repFlPath)) { MessageBox.Show("اختر ملف .zip أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string msg = "ستطبّق حزمة التحديث عبر adb sideload:\r\n" + Path.GetFileName(repFlPath) +
                         "\r\n\r\n• يجب أن يكون الجهاز في وضع Recovery ← «Apply update from ADB».\r\n• بعض الحزم قد تمسح بيانات حسب نوعها.\r\n\r\nمتابعة؟";
            if (MessageBox.Show(msg, "تأكيد sideload", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            SetTb(repFlResult, "جاري: adb sideload ...\r\nلا تفصل الجهاز.");
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "sideload " + Shell.Q(repFlPath), 1800000);
                SetTb(repFlResult, "═══ نتيجة adb sideload ═══\r\n\r\n" + (r.outp ?? "") + "\r\n\r\n" + (r.ok ? "✔ انتهى." : "✗ فشل — راجع المخرجات."));
                LogOp("sideload");
            });
        }

        // دليل التفليش حسب المعالج (إرشاد — خارج البرنامج بأداة الشركة)
        void FlGuide(string chip) { SetTb(repFlResult, FlGuideText(chip)); }

        // نص دليل التفليش — قابل لإعادة الاستخدام في أي قسم
        string FlGuideText(string chip)
        {
            var sb = new StringBuilder();
            string model = "", build = "";
            var d = devices.FirstOrDefault(x => x.Kind == "android");
            if (d != null)
            {
                model = Devices.Prop(d, "ro.product.model");
                build = Devices.Prop(d, "ro.build.display.id");
            }
            sb.AppendLine("═══ دليل التفليش حسب الماركة/المعالج ═══");
            sb.AppendLine("");
            if (model != "" || build != "")
            {
                sb.AppendLine("● بيانات جهازك (أرسلها للدعم لجلب الفيرموير المطابق):");
                if (model != "") sb.AppendLine("   الموديل: " + model);
                if (build != "") sb.AppendLine("   رقم البناء: " + build);
                sb.AppendLine("");
            }
            else
            {
                sb.AppendLine("ℹ الجهاز غير متصل بـADB (طبيعي بوضع CrashDump/Download). خذ الموديل ورقم البناء من ملصق الجهاز أو شاشة الريكفري.");
                sb.AppendLine("");
            }

            switch (chip)
            {
                case "samsung":
                    sb.AppendLine("● Samsung  (تفليش Odin — ملفات منفصلة BL/AP/CP/CSC)");
                    sb.AppendLine("   الأداة: Odin (ويندوز) أو Thor (متعدد المنصات)");
                    sb.AppendLine("   التعريفات: Samsung USB Driver  |  الصيغة: ملفات .tar.md5 داخل أرشيف الفيرموير");
                    sb.AppendLine("");
                    sb.AppendLine("   خانات Odin وما يوضع فيها:");
                    sb.AppendLine("   • BL  = Bootloader (ملف يبدأ بـ BL_...)");
                    sb.AppendLine("   • AP  = النظام الأساسي (AP_... وهو الأكبر حجماً)");
                    sb.AppendLine("   • CP  = المودم/الاتصالات (CP_...)");
                    sb.AppendLine("   • CSC = ملف المنطقة — نوعان:");
                    sb.AppendLine("       - CSC_...  يمسح البيانات (Factory).");
                    sb.AppendLine("       - HOME_CSC_...  يحافظ على البيانات قدر الإمكان (مفضّل عند التحديث فقط).");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) ثبّت Samsung USB Driver على الكمبيوتر.");
                    sb.AppendLine("   2) افتح Odin، وضع كل ملف بخانته (BL/AP/CP/CSC).");
                    sb.AppendLine("   3) الإعدادات: فعّل Auto Reboot و F.Reset Time فقط. اترك Re-Partition و Nand Erase غير مفعّلة.");
                    sb.AppendLine("   4) أدخل الجهاز Download Mode: أطفئه ثم امسك (خفض الصوت + Bixby/رفع الصوت) ووصّل USB، ثم رفع الصوت للتأكيد.");
                    sb.AppendLine("   5) تأكد من ظهور منفذ أزرق/COM في Odin ← اضغط Start ← انتظر PASS! الأخضر.");
                    break;
                case "xiaomi":
                    sb.AppendLine("● Xiaomi / Redmi / POCO");
                    sb.AppendLine("   الأداة: Mi Flash Tool (فيرموير fastboot صيغة .tgz) أو أوامر fastboot مباشرة.");
                    sb.AppendLine("   التعريفات: Mi USB / Google USB Driver  |  الصيغة: حزمة fastboot فيها flash_all.bat");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) يتطلب غالباً فتح البوتلودر (Mi Unlock + حساب Mi — يمسح البيانات، بموافقة المالك فقط).");
                    sb.AppendLine("   2) أدخل الجهاز Fastboot: أطفئه ثم امسك (خفض الصوت + Power).");
                    sb.AppendLine("   3) افتح Mi Flash، اختر مجلد الفيرموير، واختر الوضع الآمن «clean all» (ليس lock) ← flash.");
                    sb.AppendLine("   4) أو من تبويب Firmware Flashing بالبرنامج: اختر القسم و«fastboot flash».");
                    break;
                case "oppo":
                    sb.AppendLine("● Oppo / Realme / OnePlus");
                    sb.AppendLine("   الطرق: تحديث رسمي (ملف .ozip/ .ofp عبر الإعدادات) — أو fastboot للموديلات المفتوحة — أو MsmDownloadTool (أداة مركز خدمة مقيّدة بحساب).");
                    sb.AppendLine("   التعريفات: Qualcomm/MTK حسب المعالج  |  الصيغة: .ozip / .ofp / حزمة fastboot");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات (الطريقة الرسمية الآمنة):");
                    sb.AppendLine("   1) انسخ ملف .ozip لذاكرة الجهاز ← الإعدادات ← تحديث النظام ← من القائمة اختر الملف محلياً.");
                    sb.AppendLine("   2) لموديلات OnePlus المفتوحة: fastboot/ MSM. تجنّب أدوات الصندوق التي تتجاوز الحسابات.");
                    break;
                case "vivo":
                    sb.AppendLine("● Vivo / iQOO");
                    sb.AppendLine("   الطرق: تحديث رسمي داخلي، أو أدوات مركز خدمة Vivo الرسمية (مقيّدة). fastboot للموديلات المفتوحة.");
                    sb.AppendLine("   التعريفات: Vivo USB / Qualcomm-MTK حسب المعالج  |  الصيغة: حزمة الشركة");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات: استخدم ملف الفيرموير الرسمي المطابق تماماً للـPD/موديل، واتبع أداة Vivo الرسمية. لا تتجاوز أي قفل حساب.");
                    break;
                case "huawei":
                    sb.AppendLine("● Huawei / Honor");
                    sb.AppendLine("   الطرق: eRecovery (استرجاع عبر الإنترنت) — HiSuite للتحديث/الاسترجاع — dload (مجلد على SD).");
                    sb.AppendLine("   ملاحظة: البوتلودر مقفول رسمياً على الأجهزة الحديثة، والتفليش العميق يتطلب أدوات/كروت مقيّدة.");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات (الآمنة):");
                    sb.AppendLine("   1) HiSuite على الكمبيوتر ← System Recovery لإعادة تثبيت النظام الرسمي.");
                    sb.AppendLine("   2) أو eRecovery: امسك (رفع الصوت + Power) مع الشاحن ← Download latest and recovery عبر الواي فاي.");
                    break;
                case "moto":
                    sb.AppendLine("● Motorola / Lenovo / Nokia / Google Pixel");
                    sb.AppendLine("   الأداة: Fastboot (أوامر flash) — Pixel له أداة ويب Android Flash Tool الرسمية — Lenovo له Lenovo/Motorola Smart Assistant (LMSA/Rescue).");
                    sb.AppendLine("   التعريفات: Google USB Driver  |  الصيغة: حزمة factory image فيها flashall / flash-all");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) أدخل الجهاز Bootloader: امسك (خفض الصوت + Power).");
                    sb.AppendLine("   2) Pixel: افتح Android Flash Tool من المتصفح الرسمي ووصّل الجهاز ← اتبع الخطوات.");
                    sb.AppendLine("   3) Motorola/Nokia: شغّل flashall، أو من تبويب Firmware Flashing بالبرنامج «fastboot flash» لكل قسم.");
                    sb.AppendLine("   4) Lenovo (هواتف/تابلت): استخدم Lenovo Rescue/LMSA للاسترجاع الرسمي، أو fastboot للموديلات المفتوحة.");
                    break;
                case "tecno":
                    sb.AppendLine("● Tecno / Infinix / itel  (غالباً MediaTek)");
                    sb.AppendLine("   الأداة: SP Flash Tool  |  التعريفات: MTK VCOM  |  الصيغة: scatter (.txt) + صور الأقسام");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) ثبّت تعريفات MTK VCOM.");
                    sb.AppendLine("   2) افتح SP Flash Tool ← Scatter-loading ← scatter.txt ← Download Only (ليس Format All).");
                    sb.AppendLine("   3) الجهاز مطفأ ← اضغط Download ← وصّل USB ← انتظر الدائرة الخضراء.");
                    sb.AppendLine("   ملاحظة: بعض موديلات itel/Infinix معالجها Unisoc — عندها استخدم ResearchDownload (اختر Unisoc/SPD).");
                    break;
                case "lg":
                    sb.AppendLine("● LG");
                    sb.AppendLine("   الأداة: LGUP (مع LG Mobile Driver)  |  الصيغة: .kdz / .tot");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) ثبّت LG United Mobile Driver + LGUP.");
                    sb.AppendLine("   2) أدخل الجهاز Download Mode: أطفئه ثم امسك «رفع الصوت» ووصّل USB.");
                    sb.AppendLine("   3) افتح LGUP ← اختر .kdz ← Upgrade (يحافظ على البيانات) أو Refurbish (يمسح).");
                    break;
                case "iphone":
                    sb.AppendLine("● iPhone / iPad  (Apple)");
                    sb.AppendLine("   الأداة: iTunes (ويندوز) أو Finder (ماك) — بديل: 3uTools.");
                    sb.AppendLine("   الصيغة: .ipsw (رسمي من Apple فقط، موقّع)");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) وصّل الجهاز ← افتح iTunes/Finder.");
                    sb.AppendLine("   2) Update = يحدّث دون مسح. Restore = يعيد ضبط المصنع (يمسح البيانات).");
                    sb.AppendLine("   3) لو ما يستجيب: ادخل Recovery/DFU حسب الموديل ثم Restore.");
                    sb.AppendLine("   ⛔ تنبيه: لا نتجاوز قفل التفعيل iCloud (Activation Lock) — يتطلب حساب المالك أو فاتورة رسمية.");
                    break;
                case "spd":
                    sb.AppendLine("● Unisoc / SPD  (علامتها: CrashDump / TigerDump)");
                    sb.AppendLine("   الأداة: ResearchDownload (أو SPD Upgrade Tool)");
                    sb.AppendLine("   التعريفات: SPD Drivers  |  صيغة الملف: .pac");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) ثبّت تعريفات SPD على الكمبيوتر.");
                    sb.AppendLine("   2) افتح ResearchDownload ← أيقونة الترس ← اختر ملف .pac.");
                    sb.AppendLine("   3) اضغط زر البدء ▶.");
                    sb.AppendLine("   4) والتابلت مطفأ تماماً، امسك «خفض الصوت» ووصّل كيبل USB.");
                    sb.AppendLine("   5) ينزل الشريط تلقائياً ← انتظر Passed الأخضر ← افصل وشغّل.");
                    break;
                case "rk":
                    sb.AppendLine("● Rockchip");
                    sb.AppendLine("   الأداة: RKDevTool  |  التعريفات: DriverAssistant  |  الصيغة: .img");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) ثبّت DriverAssistant على الكمبيوتر.");
                    sb.AppendLine("   2) افتح RKDevTool واختر ملف .img.");
                    sb.AppendLine("   3) أطفئ التابلت (من الريكفري: Power off).");
                    sb.AppendLine("   4) امسك «خفض الصوت» (أو رفع الصوت حسب التعليمات) ووصّل USB حتى يظهر «Found One LOADER Device».");
                    sb.AppendLine("   5) اضغط Upgrade (أو Restore) وانتظر حتى يكتمل ويعيد التشغيل.");
                    break;
                case "mtk":
                    sb.AppendLine("● MediaTek (MTK)");
                    sb.AppendLine("   الأداة: SP Flash Tool  |  التعريفات: MTK VCOM/Preloader  |  الصيغة: scatter (.txt) + صور الأقسام");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) ثبّت تعريفات MTK VCOM.");
                    sb.AppendLine("   2) افتح SP Flash Tool ← Scatter-loading ← اختر ملف scatter.txt.");
                    sb.AppendLine("   3) اختر Download Only (ليس Format All — يتجنّب مسح غير ضروري).");
                    sb.AppendLine("   4) اضغط Download، والتابلت مطفأ، وصّل USB (أحياناً مع زر خفض الصوت).");
                    sb.AppendLine("   5) انتظر دائرة خضراء (Download OK) ← افصل وشغّل.");
                    break;
                case "allwinner":
                    sb.AppendLine("● Allwinner  (معالجات A-series، شائع بالتابلت الرخيص)");
                    sb.AppendLine("   الأداة: PhoenixSuit (هواتف/تابلت) أو LiveSuit — و PhoenixCard لعمل كرت SD إقلاعي.");
                    sb.AppendLine("   التعريفات: Allwinner USB Driver (usb_driver)  |  الصيغة: .img (فيرموير Allwinner)");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) ثبّت تعريفات Allwinner USB على الكمبيوتر.");
                    sb.AppendLine("   2) افتح PhoenixSuit ← Firmware ← اختر ملف .img ← Upgrade.");
                    sb.AppendLine("   3) والجهاز مطفأ: امسك زر الصوت (أو زر FEL حسب الموديل) ووصّل USB، واضغط Power عدة مرات لدخول وضع FEL.");
                    sb.AppendLine("   4) أكّد المسح عند الطلب ← انتظر حتى يكتمل 100% ← افصل وشغّل.");
                    sb.AppendLine("   بديل: PhoenixCard ← اكتب الفيرموير على كرت SD (Startup) ← أدخله وشغّل للإقلاع/التفليش.");
                    break;
                default: // qcom
                    sb.AppendLine("● Qualcomm (EDL 9008)");
                    sb.AppendLine("   الأداة: QFIL (أو QPST)  |  التعريفات: Qualcomm USB (QDLoader 9008)  |  الصيغة: rawprogram + صور");
                    sb.AppendLine("");
                    sb.AppendLine("   الخطوات:");
                    sb.AppendLine("   1) ثبّت تعريفات Qualcomm QDLoader 9008.");
                    sb.AppendLine("   2) افتح QFIL ← اختر Programmer (prog_emmc_firehose...) و rawprogram/patch.");
                    sb.AppendLine("   3) أدخل الجهاز وضع EDL (9008) ووصّل USB.");
                    sb.AppendLine("   4) اضغط Download ← انتظر Finish ← افصل وشغّل.");
                    break;
            }

            sb.AppendLine("");
            sb.AppendLine("⛔ مهم: الفيرموير لازم يطابق الموديل بالضبط (تفليش غلط = بريك). التفليش يمسح البيانات — خذ نسخة احتياطية إن أمكن.");
            sb.AppendLine("هذا إرشاد فقط — التفليش يتم بأداة الشركة خارج البرنامج، ولا نتجاوز أي قفل حساب (FRP / iCloud / حساب Mi).");
            return sb.ToString();
        }

        // ============ قسم السوفت وير — مركز شامل ============
        void BuildSoftPage()
        {
            pageSoft = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageSoft);

            var title = new Label { Text = "💿 السوفت وير — Software", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageSoft.Controls.Add(title);
            Tr(title, "💿 السوفت وير — Software", "💿 Software Center");

            var note = new Label
            {
                Text = "عرض معلومات النظام، تطبيق تحديث OTA، إدارة التطبيقات، والتفليش — بمبدأ الأمان (لا مسح/تفليش تلقائي، تأكيد يدوي لأي عملية).",
                ForeColor = Color.White, BackColor = Color.FromArgb(30, 60, 110), Location = new Point(20, 46), Size = new Size(675, 36),
                TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9), Padding = new Padding(8, 0, 8, 0)
            };
            pageSoft.Controls.Add(note);

            var infoBtn = new Button { Text = "🔍 فحص معلومات السوفت وير", Location = new Point(20, 88), Size = new Size(330, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            infoBtn.FlatAppearance.BorderSize = 0; infoBtn.Click += (s, e) => SoftInfo();
            pageSoft.Controls.Add(infoBtn);
            Tr(infoBtn, "🔍 فحص معلومات السوفت وير", "🔍 Scan Software Info");

            var otaPick = new Button { Text = "📂 اختيار تحديث .zip", Location = new Point(360, 88), Size = new Size(160, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            otaPick.FlatAppearance.BorderColor = BLUE; otaPick.Click += (s, e) => SoftOtaPick();
            pageSoft.Controls.Add(otaPick);

            var otaApply = new Button { Text = "📥 تطبيق OTA", Location = new Point(525, 88), Size = new Size(170, 32), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            otaApply.FlatAppearance.BorderSize = 0; otaApply.Click += (s, e) => SoftOtaApply();
            pageSoft.Controls.Add(otaApply);

            var appsBtn = new Button { Text = "📦 إدارة التطبيقات", Location = new Point(20, 126), Size = new Size(330, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            appsBtn.FlatAppearance.BorderColor = BLUE; appsBtn.Click += (s, e) => ShowPage("apps");
            pageSoft.Controls.Add(appsBtn);

            var flashBtn = new Button { Text = "⚡ تفليش / إعادة تثبيت النظام", Location = new Point(360, 126), Size = new Size(335, 32), BackColor = Color.FromArgb(150, 90, 20), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            flashBtn.FlatAppearance.BorderSize = 0; flashBtn.Click += (s, e) => ShowPage("samsung");
            pageSoft.Controls.Add(flashBtn);

            // صف البحث عن ملف التفليش
            var softSearchBtn = new Button { Text = "🔎 بحث عن ملف التفليش", Location = new Point(20, 164), Size = new Size(230, 32), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            softSearchBtn.FlatAppearance.BorderSize = 0; softSearchBtn.Click += (s, e) => SoftSearchFirmware();
            pageSoft.Controls.Add(softSearchBtn);
            softModel = new TextBox { Location = new Point(260, 165), Size = new Size(435, 30), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Consolas", 10) };
            pageSoft.Controls.Add(softModel);

            softResult = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(120, 190, 255), Location = new Point(20, 204), Size = new Size(675, 438), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            softResult.Text = "اضغط «فحص معلومات السوفت وير» لعرض تفاصيل النظام الكاملة.\r\n\r\n• تحديث OTA: اختر ملف .zip ثم ضع الجهاز في Recovery ← Apply update from ADB ← «تطبيق OTA».\r\n• التطبيقات: فتح قسم إدارة التطبيقات.\r\n• التفليش: فتح مركز الإصلاح ← Firmware Flashing.";
            pageSoft.Controls.Add(softResult);
        }

        // معلومات السوفت وير التفصيلية (قراءة فقط)
        void SoftInfo()
        {
            SetTb(softResult, "جاري قراءة معلومات النظام...");
            Task.Run(() =>
            {
                var d = devices.FirstOrDefault(x => x.Kind == "android");
                if (d == null) { SetTb(softResult, "✖ لا يوجد جهاز أندرويد متصل بـADB.\r\nوصّل الهاتف وفعّل «تصحيح USB» ثم أعد الفحص."); return; }
                var sb = new StringBuilder();
                sb.AppendLine("═══ معلومات السوفت وير ═══");
                sb.AppendLine("");
                sb.AppendLine("● الجهاز:");
                sb.AppendLine("   الشركة:   " + Devices.Prop(d, "ro.product.manufacturer"));
                sb.AppendLine("   الموديل:  " + Devices.Prop(d, "ro.product.model"));
                sb.AppendLine("   الاسم:    " + Devices.Prop(d, "ro.product.device") + " / " + Devices.Prop(d, "ro.product.board"));
                sb.AppendLine("   الشريحة:  " + Devices.Prop(d, "ro.hardware") + " " + Devices.Prop(d, "ro.board.platform"));
                sb.AppendLine("");
                sb.AppendLine("● النظام:");
                sb.AppendLine("   أندرويد:  " + Devices.Prop(d, "ro.build.version.release") + " (SDK " + Devices.Prop(d, "ro.build.version.sdk") + ")");
                sb.AppendLine("   البناء:   " + Devices.Prop(d, "ro.build.display.id"));
                sb.AppendLine("   رقم البناء: " + Devices.Prop(d, "ro.build.id"));
                sb.AppendLine("   ترقيع الأمان: " + Devices.Prop(d, "ro.build.version.security_patch"));
                string bb = Devices.Prop(d, "gsm.version.baseband"); if (string.IsNullOrWhiteSpace(bb)) bb = Devices.Prop(d, "ro.baseband");
                sb.AppendLine("   الباند (Baseband): " + (string.IsNullOrWhiteSpace(bb) ? "—" : bb));
                string kernel = Shell.Run("adb", "-s " + d.Id + " shell uname -r", 15000).outp;
                sb.AppendLine("   الكيرنل:  " + Trim(kernel));
                sb.AppendLine("");
                sb.AppendLine("● البصمة (Fingerprint):");
                sb.AppendLine("   " + Trim(Devices.Prop(d, "ro.build.fingerprint")));
                sb.AppendLine("");
                sb.AppendLine("● الحالة (معلوماتي فقط):");
                string crypto = Devices.Prop(d, "ro.crypto.state");
                sb.AppendLine("   التشفير:  " + (crypto == "encrypted" ? "مشفّر ✔" : (string.IsNullOrWhiteSpace(crypto) ? "غير معروف" : crypto)));
                string locked = Devices.Prop(d, "ro.boot.flash.locked");
                sb.AppendLine("   البوت لودر: " + (locked == "1" ? "مقفل (آمن) ✔" : locked == "0" ? "مفتوح ⚠" : "غير معروف"));
                string su = Shell.Run("adb", "-s " + d.Id + " shell which su", 15000).outp;
                sb.AppendLine("   الروت:    " + (!string.IsNullOrWhiteSpace(su) && su.Contains("/su") ? "مروّت ⚠" : "غير مروّت ✔"));
                sb.AppendLine("");
                sb.AppendLine("ملاحظة: قراءة فقط — لم يُعدّل أي شيء على الجهاز.");
                SetTb(softResult, sb.ToString());
            });
        }

        void SoftOtaPick()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "اختر ملف تحديث OTA (.zip)";
                dlg.Filter = "حزمة تحديث|*.zip|كل الملفات|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                softOtaPath = dlg.FileName;
            }
            SetTb(softResult, "ملف التحديث: " + Path.GetFileName(softOtaPath) + "\r\n\r\nضع الجهاز في Recovery ← «Apply update from ADB» ثم اضغط «تطبيق OTA».");
        }

        void SoftOtaApply()
        {
            if (softOtaPath == "" || !File.Exists(softOtaPath)) { MessageBox.Show("اختر ملف تحديث .zip أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string msg = "تطبيق تحديث OTA عبر adb sideload:\r\n" + Path.GetFileName(softOtaPath) +
                         "\r\n\r\n• يجب أن يكون الجهاز في وضع Recovery ← «Apply update from ADB».\r\n• بعض الحزم قد تمسح بيانات حسب نوعها — خذ نسخة احتياطية.\r\n\r\nمتابعة؟";
            if (MessageBox.Show(msg, "تأكيد تحديث OTA", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            SetTb(softResult, "جاري: adb sideload ...\r\nلا تفصل الجهاز.");
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "sideload " + Shell.Q(softOtaPath), 1800000);
                SetTb(softResult, "═══ نتيجة تحديث OTA ═══\r\n\r\n" + (r.outp ?? "") + "\r\n\r\n" + (r.ok ? "✔ انتهى." : "✗ فشل — تأكد أن الجهاز في وضع Apply update from ADB."));
                LogOp("تحديث OTA (sideload)");
            });
        }

        // ============ مكتبة الفيرموير — أرشفة ملفات التفليش ============
        void BuildFirmwarePage()
        {
            pageFirmware = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageFirmware);

            var title = new Label { Text = "📚 مكتبة الفيرموير", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageFirmware.Controls.Add(title);
            Tr(title, "📚 مكتبة الفيرموير", "📚 Firmware Library");

            var note = new Label { Text = "نظّم ملفات التفليش المحفوظة عندك: اختر المجلد، يعرض كل الملفات حسب النوع، واختر ملف لعرض تفاصيله والتحقق من سلامته.", ForeColor = Color.White, BackColor = Color.FromArgb(30, 60, 110), Location = new Point(20, 46), Size = new Size(675, 32), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9), Padding = new Padding(8, 0, 8, 0) };
            pageFirmware.Controls.Add(note);

            var pickBtn = new Button { Text = "📁 اختيار مجلد الفيرموير", Location = new Point(20, 84), Size = new Size(250, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            pickBtn.FlatAppearance.BorderSize = 0; pickBtn.Click += (s, e) => FwPickFolder();
            pageFirmware.Controls.Add(pickBtn);
            var openBtn = new Button { Text = "📂 فتح المجلد", Location = new Point(280, 84), Size = new Size(140, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            openBtn.FlatAppearance.BorderColor = BLUE; openBtn.Click += (s, e) => { if (fwFolder != "" && Directory.Exists(fwFolder)) OpenUrl(fwFolder); };
            pageFirmware.Controls.Add(openBtn);
            var verBtn = new Button { Text = "🔒 تحقق MD5/SHA", Location = new Point(430, 84), Size = new Size(170, 32), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            verBtn.FlatAppearance.BorderSize = 0; verBtn.Click += (s, e) => FwVerify();
            pageFirmware.Controls.Add(verBtn);
            fwCount = new Label { Text = "0 ملف", ForeColor = Color.FromArgb(159, 179, 214), BackColor = BG, Location = new Point(606, 90), Size = new Size(89, 22), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold) };
            pageFirmware.Controls.Add(fwCount);

            fwList = new ListBox { Location = new Point(20, 128), Size = new Size(340, 500), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            fwList.SelectedIndexChanged += (s, e) => FwShowDetail();
            pageFirmware.Controls.Add(fwList);

            fwDetail = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(255, 200, 120), Location = new Point(370, 128), Size = new Size(325, 500), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            fwDetail.Text = "اختر مجلداً يحتوي ملفات التفليش (.pac / .img / .zip / .tar / scatter / .bin).\r\n\r\nثم اختر ملفاً من القائمة لعرض تفاصيله.";
            pageFirmware.Controls.Add(fwDetail);
        }

        void FwPickFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "اختر مجلد ملفات التفليش";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                fwFolder = dlg.SelectedPath;
            }
            FwScan();
        }

        void FwScan()
        {
            fwPaths.Clear(); fwList.Items.Clear();
            if (fwFolder == "" || !Directory.Exists(fwFolder)) return;
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pac", ".img", ".zip", ".tar", ".md5", ".bin", ".gz", ".p5c" };
            try
            {
                foreach (var f in Directory.EnumerateFiles(fwFolder, "*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(f);
                    string name = Path.GetFileName(f).ToLower();
                    bool scatter = name.Contains("scatter") && ext.ToLower() == ".txt";
                    if (!exts.Contains(ext) && !scatter) continue;
                    long sz = 0; try { sz = new FileInfo(f).Length; } catch { }
                    fwPaths.Add(f);
                    fwList.Items.Add(Path.GetFileName(f) + "   (" + FmtSize(sz) + ")");
                }
            }
            catch { }
            fwCount.Text = fwPaths.Count + " ملف";
            SetTb(fwDetail, fwPaths.Count == 0 ? "لا توجد ملفات تفليش في هذا المجلد." : "تم العثور على " + fwPaths.Count + " ملف. اختر ملفاً لعرض تفاصيله.");
        }

        void FwShowDetail()
        {
            int i = fwList.SelectedIndex;
            if (i < 0 || i >= fwPaths.Count) return;
            try
            {
                var fi = new FileInfo(fwPaths[i]);
                string ext = fi.Extension.ToLower();
                string type =
                    ext == ".pac" || ext == ".p5c" ? "Unisoc (SPD) — ResearchDownload" :
                    ext == ".tar" || fi.Name.ToLower().EndsWith(".tar.md5") ? "Samsung (Odin)" :
                    ext == ".img" ? "صورة قسم / Rockchip (RKDevTool أو fastboot)" :
                    ext == ".zip" ? "حزمة OTA/Recovery" :
                    fi.Name.ToLower().Contains("scatter") ? "MediaTek scatter (SP Flash Tool)" :
                    ext == ".bin" ? "ملف خام" : "غير معروف";
                var sb = new StringBuilder();
                sb.AppendLine("═══ تفاصيل الملف ═══");
                sb.AppendLine("الاسم:  " + fi.Name);
                sb.AppendLine("الحجم:  " + FmtSize(fi.Length));
                sb.AppendLine("النوع:  " + type);
                sb.AppendLine("التاريخ: " + fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                sb.AppendLine("المسار: " + fi.FullName);
                sb.AppendLine("");
                sb.AppendLine("للتحقق من السلامة اضغط «تحقق MD5/SHA».");
                SetTb(fwDetail, sb.ToString());
            }
            catch (Exception ex) { SetTb(fwDetail, "تعذّر القراءة: " + ex.Message); }
        }

        void FwVerify()
        {
            int i = fwList.SelectedIndex;
            if (i < 0 || i >= fwPaths.Count) { MessageBox.Show("اختر ملفاً من القائمة أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string path = fwPaths[i];
            SetTb(fwDetail, "جاري حساب MD5 و SHA-256 (قد يأخذ وقتاً للملفات الكبيرة)...");
            Task.Run(() =>
            {
                string md5 = HashFile(path, "md5");
                string sha = HashFile(path, "sha256");
                var sb = new StringBuilder();
                sb.AppendLine("═══ التحقق من السلامة ═══");
                sb.AppendLine("الملف: " + Path.GetFileName(path));
                sb.AppendLine("");
                sb.AppendLine("MD5:");
                sb.AppendLine("  " + md5);
                sb.AppendLine("");
                sb.AppendLine("SHA-256:");
                sb.AppendLine("  " + sha);
                sb.AppendLine("");
                sb.AppendLine("قارن البصمة بالمنشورة من مصدر الفيرموير للتأكد أنه غير تالف.");
                SetTb(fwDetail, sb.ToString());
            });
        }

        // ============ الأداء — معلومات + مسح كاش + تطبيقات الخلفية (آمن) ============
        void BuildPerfPage()
        {
            pagePerf = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pagePerf);

            var title = new Label { Text = "⚡ الأداء — Performance", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pagePerf.Controls.Add(title);
            Tr(title, "⚡ الأداء — Performance", "⚡ Performance");

            var note = new Label { Text = "تحسين آمن: معلومات الذاكرة والتخزين، مسح الكاش (لا يمسح بيانات)، وإيقاف تطبيقات الخلفية. لا حذف لأي بيانات مستخدم.", ForeColor = Color.White, BackColor = Color.FromArgb(30, 60, 110), Location = new Point(20, 46), Size = new Size(675, 32), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9), Padding = new Padding(8, 0, 8, 0) };
            pagePerf.Controls.Add(note);

            var scanBtn = new Button { Text = "🔍 فحص الأداء", Location = new Point(20, 84), Size = new Size(160, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            scanBtn.FlatAppearance.BorderSize = 0; scanBtn.Click += (s, e) => PerfScan();
            var trimBtn = new Button { Text = "🧹 مسح الكاش", Location = new Point(188, 84), Size = new Size(150, 32), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            trimBtn.FlatAppearance.BorderSize = 0; trimBtn.Click += (s, e) => PerfTrimCache();
            var appsBtn = new Button { Text = "📋 تطبيقات قيد التشغيل", Location = new Point(346, 84), Size = new Size(185, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            appsBtn.FlatAppearance.BorderColor = BLUE; appsBtn.Click += (s, e) => PerfLoadApps();
            var stopBtn = new Button { Text = "⏹ إيقاف المحدد", Location = new Point(539, 84), Size = new Size(156, 32), BackColor = Color.FromArgb(150, 90, 20), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            stopBtn.FlatAppearance.BorderSize = 0; stopBtn.Click += (s, e) => PerfStopApp();
            pagePerf.Controls.Add(scanBtn); pagePerf.Controls.Add(trimBtn); pagePerf.Controls.Add(appsBtn); pagePerf.Controls.Add(stopBtn);

            perfList = new ListBox { Location = new Point(20, 128), Size = new Size(300, 500), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            pagePerf.Controls.Add(perfList);
            perfResult = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(120, 190, 255), Location = new Point(330, 128), Size = new Size(365, 500), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            perfResult.Text = "اضغط «فحص الأداء» لعرض الذاكرة والتخزين والمعالج.\r\n«تطبيقات قيد التشغيل» يعرض تطبيقات المستخدم لإيقاف ما يعمل بالخلفية.";
            pagePerf.Controls.Add(perfResult);
        }

        void PerfScan()
        {
            SetTb(perfResult, "جاري فحص الأداء...");
            Task.Run(() =>
            {
                var d = devices.FirstOrDefault(x => x.Kind == "android");
                if (d == null) { SetTb(perfResult, "✖ لا يوجد جهاز أندرويد متصل بـADB."); return; }
                var sb = new StringBuilder();
                sb.AppendLine("═══ فحص الأداء ═══");
                sb.AppendLine("");
                // الذاكرة
                var mem = Shell.Run("adb", "-s " + d.Id + " shell cat /proc/meminfo", 15000).outp;
                long total = ToLong(Rx(mem ?? "", @"MemTotal:\s*(\d+)"));
                long avail = ToLong(Rx(mem ?? "", @"MemAvailable:\s*(\d+)"));
                if (total > 0)
                {
                    double tGB = total / 1024.0 / 1024.0, aGB = avail / 1024.0 / 1024.0;
                    int usedPct = (int)Math.Round((total - avail) * 100.0 / total);
                    sb.AppendLine("● الذاكرة (RAM):");
                    sb.AppendLine("   الكلي: " + tGB.ToString("0.0") + " GB   |   متاح: " + aGB.ToString("0.0") + " GB   |   مستخدم: " + usedPct + "%");
                    sb.AppendLine("");
                }
                // التخزين
                var st = Devices.Storage(d);
                if (st.tot > 0)
                {
                    sb.AppendLine("● التخزين:");
                    sb.AppendLine("   الكلي: " + st.tot.ToString("0.0") + " GB   |   متاح: " + st.free.ToString("0.0") + " GB   |   ممتلئ: " + st.pct + "%");
                    sb.AppendLine("");
                }
                // المعالج
                string cores = Trim(Shell.Run("adb", "-s " + d.Id + " shell nproc", 10000).outp);
                sb.AppendLine("● المعالج:");
                sb.AppendLine("   " + Devices.Prop(d, "ro.hardware") + " " + Devices.Prop(d, "ro.board.platform") + "   |   الأنوية: " + (cores == "" ? "?" : cores));
                sb.AppendLine("");
                // البطارية ووقت التشغيل
                string lvl = Rx(Shell.Run("adb", "-s " + d.Id + " shell dumpsys battery", 15000).outp, @"level:\s*(\d+)");
                sb.AppendLine("● البطارية: " + (lvl == "" ? "?" : lvl + "%"));
                string up = Trim(Shell.Run("adb", "-s " + d.Id + " shell uptime", 10000).outp);
                if (up != "") sb.AppendLine("● مدة التشغيل: " + up);
                sb.AppendLine("");
                int third = 0; foreach (var l in (Shell.Run("adb", "-s " + d.Id + " shell pm list packages -3", 20000).outp ?? "").Split('\n')) if (l.Contains("package:")) third++;
                sb.AppendLine("● تطبيقات المستخدم المثبّتة: " + third);
                sb.AppendLine("");
                sb.AppendLine("نصائح آمنة: «مسح الكاش» يحرّر مساحة بدون حذف بيانات. أوقف تطبيقات الخلفية الثقيلة من القائمة.");
                SetTb(perfResult, sb.ToString());
            });
        }

        void PerfTrimCache()
        {
            var d = devices.FirstOrDefault(x => x.Kind == "android");
            if (d == null) { MessageBox.Show("لا يوجد جهاز أندرويد متصل بـADB.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (MessageBox.Show("مسح الكاش يحرّر مساحة ويُسرّع الجهاز.\r\nلا يمسح أي بيانات مستخدم (صور/حسابات/رسائل).\r\n\r\nمتابعة؟", "تأكيد مسح الكاش", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            SetTb(perfResult, "جاري مسح الكاش...");
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + d.Id + " shell pm trim-caches 999999999999", 60000);
                SetTb(perfResult, "═══ مسح الكاش ═══\r\n\r\n" + (string.IsNullOrWhiteSpace(r.outp) ? "✔ تم طلب تحرير الكاش عبر النظام (بدون حذف بيانات)." : r.outp) + "\r\n\r\n✔ انتهى. أعد «فحص الأداء» لرؤية المساحة المتحرّرة.");
                LogOp("مسح الكاش");
            });
        }

        void PerfLoadApps()
        {
            perfPkgs.Clear(); perfList.Items.Clear();
            SetTb(perfResult, "جاري تحميل تطبيقات المستخدم...");
            Task.Run(() =>
            {
                var d = devices.FirstOrDefault(x => x.Kind == "android");
                if (d == null) { SetTb(perfResult, "✖ لا يوجد جهاز متصل بـADB."); return; }
                var outp = Shell.Run("adb", "-s " + d.Id + " shell pm list packages -3", 20000).outp ?? "";
                var pkgs = new List<string>();
                foreach (var l in outp.Replace("\r", "").Split('\n'))
                {
                    var t = l.Trim(); if (!t.StartsWith("package:")) continue;
                    pkgs.Add(t.Substring("package:".Length).Trim());
                }
                pkgs.Sort();
                if (perfList.InvokeRequired) perfList.BeginInvoke(new Action(() => { perfPkgs = pkgs; foreach (var p in pkgs) perfList.Items.Add(p); }));
                else { perfPkgs = pkgs; foreach (var p in pkgs) perfList.Items.Add(p); }
                SetTb(perfResult, "تم تحميل " + pkgs.Count + " تطبيق مستخدم.\r\nاختر تطبيقاً واضغط «إيقاف المحدد» لإيقافه من الخلفية (لا يحذف بياناته).");
            });
        }

        void PerfStopApp()
        {
            int i = perfList.SelectedIndex;
            if (i < 0 || i >= perfPkgs.Count) { MessageBox.Show("اختر تطبيقاً من القائمة أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var d = devices.FirstOrDefault(x => x.Kind == "android");
            if (d == null) { MessageBox.Show("لا يوجد جهاز متصل بـADB.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string pkg = perfPkgs[i];
            if (MessageBox.Show("إيقاف التطبيق من الخلفية:\r\n" + pkg + "\r\n\r\nيوقف تشغيله فقط — لا يحذف التطبيق ولا بياناته.\r\nمتابعة؟", "تأكيد الإيقاف", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            SetTb(perfResult, "جاري إيقاف: " + pkg + " ...");
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + d.Id + " shell am force-stop " + pkg, 20000);
                SetTb(perfResult, "✔ تم إيقاف " + pkg + " من الخلفية.\r\n" + Trim(r.outp));
                LogOp("إيقاف تطبيق خلفية: " + pkg);
            });
        }

        static long ToLong(string s) { long v; return long.TryParse((s ?? "").Trim(), out v) ? v : 0; }

        void BuildUsbPage()
        {
            pageUsb = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageUsb);

            var title = new Label { Text = "فحص وإصلاح اتصال USB", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageUsb.Controls.Add(title);
            Tr(title, "فحص وإصلاح اتصال USB", "USB Connection Check & Fix");

            usbScanBtn = new Button { Text = "🔍 فحص USB بدون ADB", Location = new Point(20, 48), Size = new Size(205, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            usbScanBtn.FlatAppearance.BorderSize = 0; usbScanBtn.Click += (s, e) => UsbScan();
            pageUsb.Controls.Add(usbScanBtn);
            Tr(usbScanBtn, "🔍 فحص USB بدون ADB", "🔍 USB Check (No ADB)");

            usbToolsBtn = new Button { Text = "🧰 فحص أدوات Android / iPhone", Location = new Point(232, 48), Size = new Size(235, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            usbToolsBtn.FlatAppearance.BorderColor = BLUE; usbToolsBtn.Click += (s, e) => ShowToolReadiness();
            pageUsb.Controls.Add(usbToolsBtn);
            Tr(usbToolsBtn, "🧰 فحص أدوات Android / iPhone", "🧰 Check Android / iPhone Tools");

            usbSaveBtn = new Button { Text = "💾 حفظ التقرير", Location = new Point(474, 48), Size = new Size(221, 32), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            usbSaveBtn.FlatAppearance.BorderSize = 0; usbSaveBtn.Click += (s, e) => SaveDiagnosticReport();
            pageUsb.Controls.Add(usbSaveBtn);
            Tr(usbSaveBtn, "💾 حفظ التقرير", "💾 Save Report");

            usbResult = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(120, 190, 255), Location = new Point(20, 92), Size = new Size(675, 230), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            pageUsb.Controls.Add(usbResult);

            var gtitle = new Label { Text = "🛠 إرشادات الإصلاح", ForeColor = Color.FromArgb(201, 162, 39), BackColor = BG, Location = new Point(20, 330), Size = new Size(675, 24), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 12, FontStyle.Bold) };
            pageUsb.Controls.Add(gtitle);
            var guide = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = CARD, ForeColor = Color.White, Location = new Point(20, 358), Size = new Size(675, 284), Font = new Font("Arial", 10), BorderStyle = BorderStyle.None };
            guide.Text =
                "إذا لم يتعرّف ويندوز على الهاتف، جرّب بالترتيب:\r\n\r\n" +
                "1) على الموبايل: اسحب شريط الإشعارات ← اضغط إشعار «شحن هذا الجهاز عبر USB» ← اختر «نقل الملفات (MTP)».\r\n\r\n" +
                "2) جرّب كيبل آخر — بعض الكيبلات للشحن فقط ولا تنقل بيانات. وجرّب منفذ USB آخر (يفضّل منفذ خلفي بالكيسة).\r\n\r\n" +
                "3) إذا ظهر «جهاز غير معروف» أو علامة صفراء: افتح Device Manager (مدير الأجهزة) ← كلك يمين على الجهاز ← Update driver.\r\n\r\n" +
                "4) إن كان آيفون: ثبّت iTunes/Apple Devices أول، واضغط «الوثوق بهذا الكمبيوتر» على الآيفون.\r\n\r\n" +
                "5) أعد تشغيل الموبايل والكمبيوتر، ثم أعد الفحص.\r\n\r\n" +
                "ملاحظة: PTP = وضع الكاميرا (صور فقط) — غيّره إلى «نقل الملفات» للوصول الكامل.";
            pageUsb.Controls.Add(guide);
        }

        void SetUsbResult(string t) { if (usbResult.InvokeRequired) usbResult.BeginInvoke(new Action(() => usbResult.Text = t)); else usbResult.Text = t; }

        bool ToolExists(string name)
        {
            var r = Shell.Run("where.exe", name, 10000);
            return r.ok && !string.IsNullOrWhiteSpace(r.outp);
        }

        void ShowToolReadiness()
        {
            usbToolsBtn.Enabled = false;
            SetUsbResult("جاري فحص بيئة التشغيل والأدوات المطلوبة...\r\n");
            Task.Run(() =>
            {
                bool adb = ToolExists("adb.exe") || ToolExists("adb");
                bool pmd = ToolExists("pymobiledevice3.exe") || ToolExists("pymobiledevice3");
                bool idevice = ToolExists("idevice_id.exe") || ToolExists("idevice_id");
                bool powershell = ToolExists("powershell.exe");
                var sb = new StringBuilder();
                sb.AppendLine("══════════ تقرير جاهزية CENTER POINT TRANSFER ══════════");
                sb.AppendLine("Android: " + (adb ? "✔ ADB متوفر - يمكن استخدام وظائف ADB بعد موافقة الهاتف" : "✖ ADB غير موجود - وظائف ADB لن تعمل حتى تثبيته أو وضعه بجانب البرنامج"));
                sb.AppendLine("iPhone / iPad: " + (pmd ? "✔ pymobiledevice3 متوفر" : idevice ? "✔ idevice_id متوفر كمسار اكتشاف بديل" : "⚠ أدوات اكتشاف iPhone غير مكتشفة"));
                sb.AppendLine("Windows PowerShell: " + (powershell ? "✔ متوفر - مطلوب لفحص USB عبر Windows" : "✖ غير مكتشف"));
                sb.AppendLine("");
                sb.AppendLine("ملاحظات التوافق:");
                sb.AppendLine("• اكتشاف iPhone لا يعني أن جميع أنواع البيانات قابلة للقراءة أو النقل؛ ذلك يعتمد على تعريفات Apple والثقة والصلاحيات.");
                sb.AppendLine("• نقل ملفات Android عبر MTP يعمل بشكل منفصل عن ADB.");
                sb.AppendLine("• عمليات ADB تتطلب تفعيل تصحيح USB وموافقة صاحب الهاتف.");
                sb.AppendLine("• لا يحاول البرنامج تجاوز قفل الشاشة أو حماية الحسابات.");
                sb.AppendLine("");
                sb.AppendLine("النتيجة: " + ((adb || pmd || idevice) ? "تم العثور على أداة واحدة على الأقل." : "لم يتم العثور على أدوات ربط الهواتف؛ ثبّت الأدوات الرسمية المطلوبة."));
                SetUsbResult(sb.ToString());
                if (usbToolsBtn != null && !usbToolsBtn.IsDisposed) usbToolsBtn.BeginInvoke(new Action(() => usbToolsBtn.Enabled = true));
            });
        }

        void SaveDiagnosticReport()
        {
            string content = usbResult == null ? "لا يوجد تقرير بعد. نفّذ الفحص أولاً." : usbResult.Text;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "حفظ تقرير تشخيص Center Point";
                dlg.Filter = "Text Report (*.txt)|*.txt";
                dlg.FileName = "CenterPoint_Diagnostic_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dlg.FileName, "CENTER POINT TRANSFER V7.2 PRO\r\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n\r\n" + content, new UTF8Encoding(true));
                    LogOp("تم حفظ تقرير تشخيص USB");
                    MessageBox.Show("تم حفظ التقرير بنجاح.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { ErrBox("تعذّر حفظ التقرير: " + ex.Message); }
            }
        }

        void UsbScan()
        {
            SetUsbResult("جاري الفحص الشامل...");
            Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("═══ فحص USB الشامل ═══");
                sb.AppendLine("");

                // 1) حالة ADB الفعلية للأندرويد
                sb.AppendLine("● حالة اتصال ADB (أندرويد):");
                var ad = Shell.Run("adb", "devices -l", 30000);
                if (!ad.ok)
                    sb.AppendLine("   ✗ ADB غير مثبّت أو لا يعمل — ثبّته أو ضعه بجانب البرنامج.");
                else
                {
                    int cnt = 0;
                    foreach (var ln in ad.outp.Replace("\r", "").Split('\n'))
                    {
                        var t = ln.Trim();
                        if (t == "" || t.StartsWith("List of")) continue;
                        var parts = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2) continue;
                        string id = parts[0], state = parts[1]; cnt++;
                        if (state == "device") sb.AppendLine("   ✔ " + id + " — متصل وجاهز للنقل الكامل.");
                        else if (state == "unauthorized") sb.AppendLine("   ⚠ " + id + " — غير مصرّح: افتح الهاتف واضغط «السماح بتصحيح USB».");
                        else if (state == "offline") sb.AppendLine("   ⚠ " + id + " — غير متصل (offline): افصل الكيبل وأعد توصيله.");
                        else sb.AppendLine("   • " + id + " — الحالة: " + state);
                    }
                    if (cnt == 0) sb.AppendLine("   ✗ لا جهاز عبر ADB — فعّل «تصحيح USB (USB Debugging)» على الهاتف.");
                }
                sb.AppendLine("");

                // 2) تعريفات ويندوز (أندرويد ADB / آبل / MTP-PTP / مشاكل)
                sb.AppendLine("● تعريفات ويندوز:");
                string ps = Path.Combine(Path.GetTempPath(), "cp_usb.ps1");
                try
                {
                    File.WriteAllText(ps,
                        "Get-PnpDevice -PresentOnly | Where-Object { $_.Class -eq 'WPD' -or $_.Class -eq 'AndroidUsbDeviceClass' -or $_.Status -ne 'OK' -or $_.FriendlyName -match 'Android|ADB|Apple Mobile|iPhone|MTP|PTP' } | " +
                        "ForEach-Object { \"$($_.Status)|$($_.Class)|$($_.FriendlyName)\" }", new UTF8Encoding(false));
                }
                catch { }
                var r = Shell.Run("powershell", "-NoProfile -ExecutionPolicy Bypass -File " + Shell.Q(ps), 60000);
                bool mtp = false, ptp = false, androidDrv = false, appleDrv = false, anyWpd = false;
                var problems = new List<string>();
                if (r.ok)
                    foreach (var ln in r.outp.Replace("\r", "").Split('\n'))
                    {
                        var p = ln.Split('|');
                        if (p.Length < 3) continue;
                        string status = p[0].Trim(), cls = p[1].Trim(), name = p[2].Trim();
                        if (name == "") continue;
                        string nl = name.ToLower();
                        if (cls == "WPD") { anyWpd = true; if (nl.Contains("mtp")) mtp = true; if (nl.Contains("ptp")) ptp = true; }
                        if (cls == "AndroidUsbDeviceClass" || nl.Contains("adb") || nl.Contains("android composite") || nl.Contains("android bootloader")) androidDrv = true;
                        if (nl.Contains("apple mobile") || nl.Contains("iphone")) appleDrv = true;
                        if (status != "OK" && status != "")
                        {
                            string hint = status.ToLower().Contains("error") ? "مشكلة تعريف — حدّثه أو احذفه وأعد التوصيل"
                                : status.ToLower().Contains("unknown") ? "جهاز غير معروف — تعريف ناقص"
                                : "التعريف غير سليم";
                            problems.Add("   ✗ " + name + "  [" + status + "] — " + hint);
                        }
                    }
                sb.AppendLine("   " + (androidDrv ? "✔ تعريف Android ADB Interface موجود." : "ℹ تعريف Android ADB غير ظاهر (طبيعي إن لم تستخدم ADB)."));
                sb.AppendLine("   " + (appleDrv ? "✔ تعريف Apple Mobile Device موجود (للآيفون)." : "ℹ تعريف Apple غير ظاهر — ثبّت iTunes/Apple Devices للآيفون."));
                if (anyWpd) sb.AppendLine("   " + (mtp ? "✔ وضع MTP (نقل الملفات) نشط — جاهز." : ptp ? "⚠ وضع PTP (كاميرا) — غيّره إلى «نقل الملفات»." : "ℹ جهاز محمول ظاهر — اختر «نقل الملفات» على الهاتف."));
                else sb.AppendLine("   ✗ لا جهاز محمول (MTP) ظاهر لويندوز — الموبايل بوضع «شحن فقط» أو كيبل/منفذ/تعريف.");
                if (problems.Count > 0)
                {
                    sb.AppendLine("");
                    sb.AppendLine("● أجهزة فيها مشكلة تعريف:");
                    foreach (var pr in problems) sb.AppendLine(pr);
                }
                if (!r.ok)
                    sb.AppendLine("   (تعذّر تشغيل فحص التعريفات — تأكد من صلاحيات ويندوز/PowerShell.)");
                sb.AppendLine("");
                sb.AppendLine("راجع «إرشادات الإصلاح» بالأسفل لأي حالة أعلاه.");
                SetUsbResult(sb.ToString());
            });
        }

        // ============ مدير الملفات (تصفّح/تنزيل/رفع/حذف على الجهاز) ============
        void BuildFilesPage()
        {
            pageFiles = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageFiles);

            var title = new Label { Text = "مدير الملفات", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 10), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageFiles.Controls.Add(title); Tr(title, "مدير الملفات", "File Manager");

            AddLbl(pageFiles, "الجهاز:", 560, 46, 130);
            fmDev = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(250, 46), Size = new Size(300, 28), Font = new Font("Arial", 10) };
            fmDev.SelectedIndexChanged += (s, e) => { fmPath = "/sdcard"; FmLoad(); };
            pageFiles.Controls.Add(fmDev);
            var rf = new Button { Text = "🔄", Location = new Point(40, 46), Size = new Size(70, 28), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            rf.FlatAppearance.BorderSize = 0; rf.Click += (s, e) => { Refresh2(); FmLoad(); };
            pageFiles.Controls.Add(rf);

            var goBtn = new Button { Text = "اذهب", Location = new Point(40, 82), Size = new Size(90, 30), BackColor = GREEN, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            goBtn.FlatAppearance.BorderSize = 0; goBtn.Click += (s, e) => { fmPath = fmPathBox.Text.Trim(); FmLoad(); };
            pageFiles.Controls.Add(goBtn); Tr(goBtn, "اذهب", "Go");
            fmPathBox = new TextBox { Location = new Point(140, 83), Size = new Size(380, 28), Text = "/sdcard", BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.White, Font = new Font("Consolas", 10), BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.No };
            pageFiles.Controls.Add(fmPathBox);
            var up = new Button { Text = "⬆ للأعلى", Location = new Point(590, 82), Size = new Size(105, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            up.FlatAppearance.BorderColor = BLUE; up.Click += (s, e) => FmUp();
            pageFiles.Controls.Add(up); Tr(up, "⬆ للأعلى", "⬆ Up");

            fmList = new ListBox { Location = new Point(20, 120), Size = new Size(675, 430), BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.White, Font = new Font("Consolas", 11), BorderStyle = BorderStyle.None, SelectionMode = SelectionMode.MultiExtended };
            fmList.DoubleClick += (s, e) => FmOpen();
            pageFiles.Controls.Add(fmList);

            var dl = new Button { Text = "⬇ تنزيل المحدد", Location = new Point(500, 558), Size = new Size(195, 34), BackColor = GREEN, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            dl.FlatAppearance.BorderSize = 0; dl.Click += (s, e) => FmDownload();
            pageFiles.Controls.Add(dl); Tr(dl, "⬇ تنزيل المحدد", "⬇ Download");
            var ulB = new Button { Text = "⬆ رفع ملف", Location = new Point(295, 558), Size = new Size(195, 34), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            ulB.FlatAppearance.BorderSize = 0; ulB.Click += (s, e) => FmUpload();
            pageFiles.Controls.Add(ulB); Tr(ulB, "⬆ رفع ملف", "⬆ Upload");
            var del = new Button { Text = "🗑 حذف المحدد", Location = new Point(90, 558), Size = new Size(195, 34), BackColor = Color.FromArgb(150, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            del.FlatAppearance.BorderSize = 0; del.Click += (s, e) => FmDelete();
            pageFiles.Controls.Add(del); Tr(del, "🗑 حذف المحدد", "🗑 Delete");

            fmStatus = new Label { Text = "اختر الجهاز لعرض الملفات.", ForeColor = Color.FromArgb(201, 162, 39), BackColor = BG, Location = new Point(20, 598), Size = new Size(675, 20), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9) };
            pageFiles.Controls.Add(fmStatus);
            fmProg = new ProgressBar { Location = new Point(20, 624), Size = new Size(560, 14), Minimum = 0, Maximum = 100, Visible = false };
            pageFiles.Controls.Add(fmProg);
            fmCancel = new Button { Text = "⏹ إيقاف", Location = new Point(590, 620), Size = new Size(105, 24), BackColor = Color.FromArgb(150, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold), Visible = false };
            fmCancel.FlatAppearance.BorderSize = 0; fmCancel.Click += (s, e) => { fmCancelFlag = true; SetText(fmStatus, "جاري الإيقاف..."); };
            pageFiles.Controls.Add(fmCancel); Tr(fmCancel, "⏹ إيقاف", "⏹ Stop");
        }

        Device FmDevice() { if (fmDev == null || fmDev.SelectedIndex < 0 || fmDev.SelectedIndex >= devices.Count) return null; return devices[fmDev.SelectedIndex]; }

        bool FmIsProtected(string remote)
        {
            string p = (remote ?? "").TrimEnd('/'); if (p == "") p = "/";
            foreach (var pr in FmProtected) if (string.Equals(p, pr, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void FmSetBusy(bool busy)
        {
            fmBusy = busy;
            if (fmProg.InvokeRequired) { fmProg.BeginInvoke(new Action(() => { fmProg.Visible = busy; if (busy) fmProg.Value = 0; })); fmCancel.BeginInvoke(new Action(() => fmCancel.Visible = busy)); }
            else { fmProg.Visible = busy; if (busy) fmProg.Value = 0; fmCancel.Visible = busy; }
        }

        void FmSetProg(int v)
        {
            int val = v < 0 ? 0 : (v > 100 ? 100 : v);
            if (fmProg.InvokeRequired) fmProg.BeginInvoke(new Action(() => fmProg.Value = val));
            else fmProg.Value = val;
        }

        void FmLoad()
        {
            var d = FmDevice();
            if (d == null) { fmList.Items.Clear(); fmStatus.Text = "اختر الجهاز."; return; }
            if (d.Kind != "android") { fmList.Items.Clear(); fmStatus.Text = "مدير الملفات يدعم أندرويد بهذي النسخة."; return; }
            if (string.IsNullOrWhiteSpace(fmPath)) fmPath = "/sdcard";
            fmPathBox.Text = fmPath; fmStatus.Text = "جاري القراءة..."; fmList.Items.Clear();
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + d.Id + " shell ls -1pA " + Shell.Q(fmPath), 60000);
                var dirs = new List<string>(); var files = new List<string>();
                if (r.ok)
                    foreach (var ln in r.outp.Replace("\r", "").Split('\n'))
                    {
                        var t = ln.TrimEnd();
                        if (t == "" || t.Contains("Permission denied") || t.Contains("No such file")) continue;
                        if (t.EndsWith("/")) dirs.Add(t); else files.Add(t);
                    }
                dirs.Sort(); files.Sort();
                this.BeginInvoke(new Action(() =>
                {
                    fmList.Items.Clear();
                    if (fmPath != "/") fmList.Items.Add(".. (للأعلى)");
                    foreach (var dd in dirs) fmList.Items.Add("📁 " + dd);
                    foreach (var ff in files) fmList.Items.Add("📄 " + ff);
                    fmStatus.Text = r.ok ? (dirs.Count + " مجلد، " + files.Count + " ملف — " + fmPath) : "تعذّر القراءة (صلاحية أو مسار غير موجود).";
                }));
            });
        }

        void FmOpen()
        {
            if (fmList.SelectedItem == null) return;
            string it = fmList.SelectedItem.ToString();
            if (it.StartsWith("..")) { FmUp(); return; }
            if (it.StartsWith("📁 "))
            {
                string name = it.Substring(2).Trim().TrimEnd('/');
                fmPath = (fmPath == "/" ? "" : fmPath) + "/" + name;
                FmLoad();
            }
        }

        void FmUp()
        {
            if (string.IsNullOrEmpty(fmPath) || fmPath == "/") { fmPath = "/"; FmLoad(); return; }
            string p = fmPath.TrimEnd('/');
            int i = p.LastIndexOf('/');
            fmPath = i <= 0 ? "/" : p.Substring(0, i);
            FmLoad();
        }

        List<(string name, bool dir)> FmSelected()
        {
            var list = new List<(string, bool)>();
            foreach (var o in fmList.SelectedItems)
            {
                string it = o.ToString();
                if (it.StartsWith("..")) continue;
                bool dir = it.StartsWith("📁 ");
                string name = it.Substring(2).Trim().TrimEnd('/');
                if (name != "") list.Add((name, dir));
            }
            return list;
        }

        void FmDownload()
        {
            var d = FmDevice(); if (d == null || d.Kind != "android") { MessageBox.Show("اختر جهاز أندرويد."); return; }
            var sel = FmSelected(); if (sel.Count == 0) { MessageBox.Show("حدّد ملفات أو مجلدات للتنزيل."); return; }
            if (fmBusy) { MessageBox.Show("هناك عملية جارية. أوقفها أو انتظر."); return; }
            string dest;
            using (var fb = new FolderBrowserDialog()) { fb.Description = "اختر مجلد الحفظ على الكمبيوتر"; if (fb.ShowDialog() != DialogResult.OK) return; dest = fb.SelectedPath; }
            fmCancelFlag = false; FmSetBusy(true);
            SetText(fmStatus, "جاري التنزيل...");
            Task.Run(() =>
            {
                int ok = 0, fail = 0, idx = 0, totalN = sel.Count;
                var rx = new Regex(@"\[\s*(\d+)%\]");
                foreach (var e in sel)
                {
                    if (fmCancelFlag) break;
                    int cur = idx;
                    string remote = (fmPath == "/" ? "" : fmPath) + "/" + e.name;
                    SetText(fmStatus, "⬇ (" + (idx + 1) + "/" + totalN + ") " + e.name);
                    var pr = Shell.RunStream("adb", "-s " + d.Id + " pull " + Shell.Q(remote) + " " + Shell.Q(dest),
                        line => { var m = rx.Match(line); if (m.Success && int.TryParse(m.Groups[1].Value, out int pct)) FmSetProg((cur * 100 + pct) / totalN); },
                        () => fmCancelFlag, 3600000);
                    if (pr.ok) ok++; else fail++;
                    idx++;
                    FmSetProg(idx * 100 / totalN);
                }
                if (ok > 0) LogOp("تنزيل " + ok + " عنصر من الهاتف");
                FmSetBusy(false);
                this.BeginInvoke(new Action(() => { fmStatus.Text = (fmCancelFlag ? "⏹ أُوقف — " : "✅ ") + "تم التنزيل: نجح " + ok + "، فشل " + fail + " ← " + dest; }));
            });
        }

        void FmUpload()
        {
            var d = FmDevice(); if (d == null || d.Kind != "android") { MessageBox.Show("اختر جهاز أندرويد."); return; }
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "اختر ملفات للرفع إلى: " + fmPath; ofd.Multiselect = true;
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                var files = ofd.FileNames; if (files.Length == 0) return;
                if (fmBusy) { MessageBox.Show("هناك عملية جارية. أوقفها أو انتظر."); return; }
                fmCancelFlag = false; FmSetBusy(true);
                SetText(fmStatus, "جاري الرفع...");
                Task.Run(() =>
                {
                    int ok = 0, fail = 0, idx = 0, totalN = files.Length;
                    var rx = new Regex(@"\[\s*(\d+)%\]");
                    foreach (var f in files)
                    {
                        if (fmCancelFlag) break;
                        int cur = idx;
                        SetText(fmStatus, "⬆ (" + (idx + 1) + "/" + totalN + ") " + Path.GetFileName(f));
                        var pr = Shell.RunStream("adb", "-s " + d.Id + " push " + Shell.Q(f) + " " + Shell.Q(fmPath) + "/",
                            line => { var m = rx.Match(line); if (m.Success && int.TryParse(m.Groups[1].Value, out int pct)) FmSetProg((cur * 100 + pct) / totalN); },
                            () => fmCancelFlag, 3600000);
                        if (pr.ok) ok++; else fail++;
                        idx++;
                        FmSetProg(idx * 100 / totalN);
                    }
                    if (ok > 0) LogOp("رفع " + ok + " ملف للهاتف");
                    FmSetBusy(false);
                    this.BeginInvoke(new Action(() => { fmStatus.Text = (fmCancelFlag ? "⏹ أُوقف — " : "✅ ") + "تم الرفع: نجح " + ok + "، فشل " + fail; FmLoad(); }));
                });
            }
        }

        void FmDelete()
        {
            var d = FmDevice(); if (d == null || d.Kind != "android") { MessageBox.Show("اختر جهاز أندرويد."); return; }
            var sel = FmSelected(); if (sel.Count == 0) { MessageBox.Show("حدّد ما تريد حذفه."); return; }

            // حماية من المسارات الخطرة: نستبعد أي عنصر مسارهُ محميّ
            var safe = new List<(string name, bool dir)>(); var blocked = new List<string>();
            foreach (var e in sel)
            {
                string remote = (fmPath == "/" ? "" : fmPath) + "/" + e.name;
                if (FmIsProtected(remote)) blocked.Add(e.name); else safe.Add(e);
            }
            if (blocked.Count > 0)
                MessageBox.Show("مُنع حذف مسارات محميّة للنظام:\n\n" + string.Join("\n", blocked) + "\n\nهذه المسارات أساسية ولا يسمح البرنامج بحذفها.", "حماية", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (safe.Count == 0) { SetText(fmStatus, "لم يُحذف شيء (كلها مسارات محميّة)."); return; }

            string names = string.Join("\n", safe.Select(x => (x.dir ? "📁 " : "📄 ") + x.name));
            if (MessageBox.Show("⚠ سيتم حذف العناصر التالية نهائياً من الهاتف:\n\n" + names + "\n\nلا يمكن التراجع. متأكد؟",
                "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            SetText(fmStatus, "جاري الحذف...");
            Task.Run(() =>
            {
                int ok = 0, fail = 0;
                foreach (var e in safe)
                {
                    string remote = (fmPath == "/" ? "" : fmPath) + "/" + e.name;
                    var pr = Shell.Run("adb", "-s " + d.Id + " shell rm -rf " + Shell.Q(remote), 120000);
                    if (pr.ok) ok++; else fail++;
                }
                if (ok > 0) LogOp("حذف " + ok + " عنصر من الهاتف");
                this.BeginInvoke(new Action(() => { fmStatus.Text = "تم الحذف: نجح " + ok + "، فشل " + fail; FmLoad(); }));
            });
        }

        // ============ قسم التقارير (تحليل الأداء والأخطاء) ============
        void BuildReportsPage()
        {
            pageReports = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageReports);

            var title = new Label { Text = "التقارير — تحليل الأداء والأخطاء", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 10), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageReports.Controls.Add(title); Tr(title, "التقارير — تحليل الأداء والأخطاء", "Reports — Performance & Errors");

            var rf = new Button { Text = "🔄 تحديث", Location = new Point(20, 48), Size = new Size(110, 30), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            rf.FlatAppearance.BorderSize = 0; rf.Click += (s, e) => RepLoad();
            pageReports.Controls.Add(rf); Tr(rf, "🔄 تحديث", "🔄 Refresh");
            var ofb = new Button { Text = "📁 فتح مجلد التقارير", Location = new Point(138, 48), Size = new Size(170, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            ofb.FlatAppearance.BorderColor = BLUE; ofb.Click += (s, e) => RepOpenFolder();
            pageReports.Controls.Add(ofb); Tr(ofb, "📁 فتح مجلد التقارير", "📁 Open Reports Folder");
            var opn = new Button { Text = "📄 فتح المحدد", Location = new Point(316, 48), Size = new Size(140, 30), BackColor = GREEN, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            opn.FlatAppearance.BorderSize = 0; opn.Click += (s, e) => RepOpen();
            pageReports.Controls.Add(opn); Tr(opn, "📄 فتح المحدد", "📄 Open Selected");
            monBtn = new Button { Text = "🔴 بدء الفحص المستمر", Location = new Point(464, 48), Size = new Size(231, 30), BackColor = GREEN, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            monBtn.FlatAppearance.BorderSize = 0; monBtn.Click += (s, e) => MonToggle();
            pageReports.Controls.Add(monBtn); Tr(monBtn, "🔴 بدء الفحص المستمر", "🔴 Start Live Monitor");
            var selfBtn = new Button { Text = "🔍 فحص شامل (اتصال + هواتف + اختبار نقل سريع + معالجة الأخطاء)", Location = new Point(20, 82), Size = new Size(675, 32), BackColor = Color.FromArgb(130, 80, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            selfBtn.FlatAppearance.BorderSize = 0; selfBtn.Click += (s, e) => SelfTest();
            pageReports.Controls.Add(selfBtn); Tr(selfBtn, "🔍 فحص شامل (اتصال + هواتف + اختبار نقل سريع + معالجة الأخطاء)", "🔍 Full Self-Test (connection + devices + fast-transfer + errors)");
            monTimer = new System.Windows.Forms.Timer { Interval = 4000 };
            monTimer.Tick += (s, e) => MonTick();

            var sb = new Panel { Location = new Point(20, 120), Size = new Size(675, 92), BackColor = CARD };
            pageReports.Controls.Add(sb);
            var st = new Label { Text = "ملخّص آخر نقل", ForeColor = Color.FromArgb(158, 203, 255), BackColor = CARD, Location = new Point(12, 6), Size = new Size(651, 22), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 11, FontStyle.Bold) };
            sb.Controls.Add(st); Tr(st, "ملخّص آخر نقل", "Latest Transfer Summary");
            repSummary = new Label { Text = "لا توجد تقارير بعد.", ForeColor = Color.White, BackColor = CARD, Location = new Point(14, 30), Size = new Size(647, 56), TextAlign = ContentAlignment.TopRight, Font = new Font("Arial", 10) };
            sb.Controls.Add(repSummary);

            repList = new ListBox { Location = new Point(20, 222), Size = new Size(675, 394), BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.White, Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            repList.DoubleClick += (s, e) => RepOpen();
            pageReports.Controls.Add(repList);

            monStatus = new Label { Text = "تُحفظ التقارير تلقائياً بعد كل عملية. اضغط «بدء الفحص المستمر» لمراقبة حيّة للأجهزة.", ForeColor = Color.FromArgb(120, 140, 180), BackColor = BG, Location = new Point(20, 624), Size = new Size(675, 20), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9) };
            pageReports.Controls.Add(monStatus);
        }

        // الفحص المستمر (مراقبة حيّة لحالة الأجهزة/الاتصال، تتجدّد كل 4 ثوانٍ)
        void MonToggle()
        {
            monOn = !monOn;
            if (monOn)
            {
                monBtn.Text = "⏹ إيقاف الفحص المستمر"; monBtn.BackColor = Color.FromArgb(150, 50, 50);
                monLast = ""; monTimer.Start(); MonTick();
            }
            else
            {
                monBtn.Text = "🔴 بدء الفحص المستمر"; monBtn.BackColor = GREEN;
                monTimer.Stop();
                SetText(monStatus, "⏸ الفحص المستمر متوقف. اضغط «بدء الفحص المستمر» لاستئنافه.");
            }
        }

        void MonTick()
        {
            Task.Run(() =>
            {
                var ad = Shell.Run("adb", "devices", 12000);
                string stamp = DateTime.Now.ToString("HH:mm:ss");
                int n = 0; var cur = new StringBuilder();
                if (ad.ok)
                    foreach (var ln in ad.outp.Replace("\r", "").Split('\n'))
                    {
                        var t = ln.Trim(); if (t == "" || t.StartsWith("List of")) continue;
                        var p = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (p.Length < 2) continue; n++;
                        cur.Append(p[0] + ":" + p[1] + "   ");
                    }
                string state = n + " جهاز" + (cur.Length > 0 ? "  |  " + cur.ToString().Trim() : "  |  لا أجهزة متصلة");
                if (monLast != "" && monLast != state) LogError("مراقبة", "تغيّر حالة الأجهزة: [" + monLast + "] ← [" + state + "]");
                monLast = state;
                string dot = (DateTime.Now.Second % 2 == 0) ? "🔴" : "⚪";
                SetText(monStatus, dot + " فحص مستمر — " + stamp + "   |   " + state + (opBusy ? "   |   ⚙ عملية جارية: " + opName : ""));
            });
        }

        // فحص شامل: الاتصال + الهواتف + اختبار نقل سريع حقيقي + مراجعة الأخطاء
        void SelfTest()
        {
            SetText(monStatus, "🔍 جاري الفحص الشامل...");
            Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("========================================");
                sb.AppendLine("     فحص شامل - CENTER POINT TRANSFER");
                sb.AppendLine("========================================");
                sb.AppendLine("التاريخ: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("");

                // 1) الأدوات
                sb.AppendLine("● الأدوات:");
                var adbVer = Shell.Run("adb", "version", 15000);
                sb.AppendLine("   ADB: " + (adbVer.ok ? "✔ متوفر (" + Shell.AdbExe() + ")" : "✗ غير متوفر — لن تعمل وظائف أندرويد"));
                var pmd = Shell.Run("pymobiledevice3", "version", 15000);
                sb.AppendLine("   pymobiledevice3 (آيفون): " + (pmd.ok ? "✔ متوفر" : "⚠ غير متوفر (مطلوب للآيفون)"));
                sb.AppendLine("");

                // 2) الاتصال والهواتف
                sb.AppendLine("● الاتصال والهواتف:");
                var ad = Shell.Run("adb", "devices", 20000);
                var ready = new List<string>();
                int nDev = 0;
                if (ad.ok)
                    foreach (var ln in ad.outp.Replace("\r", "").Split('\n'))
                    {
                        var t = ln.Trim(); if (t == "" || t.StartsWith("List of")) continue;
                        var p = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (p.Length < 2) continue; nDev++;
                        string id = p[0], state = p[1];
                        string model = "";
                        if (state == "device") { try { model = Devices.Prop(new Device { Kind = "android", Id = id, Model = "" }, "ro.product.model"); } catch { } }
                        sb.AppendLine("   " + (state == "device" ? "✔" : "⚠") + " " + id + " — " + state + (model != "" ? " (" + model + ")" : ""));
                        if (state == "device") ready.Add(id);
                    }
                if (nDev == 0) sb.AppendLine("   ✗ لا هاتف متصل عبر ADB — فعّل «تصحيح USB» واضغط السماح.");
                sb.AppendLine("");

                // 3) اختبار النقل السريع (رحلة حقيقية صغيرة + تحقق بالحجم + تنظيف)
                sb.AppendLine("● اختبار النقل السريع:");
                if (ready.Count == 0) sb.AppendLine("   ⚠ لا يوجد جهاز جاهز لإجراء الاختبار.");
                else
                {
                    string id = ready[0];
                    try
                    {
                        string localTest = Path.Combine(Path.GetTempPath(), "cp_selftest_" + DateTime.Now.Ticks + ".txt");
                        File.WriteAllText(localTest, "CenterPoint self-test " + Guid.NewGuid().ToString("N"), new UTF8Encoding(false));
                        long lsz = new FileInfo(localTest).Length;
                        string remote = "/sdcard/.cp_selftest_" + DateTime.Now.Ticks + ".txt";
                        var push = Shell.Run("adb", "-s " + id + " push " + Shell.Q(localTest) + " " + remote, 60000);
                        var stt = Shell.Run("adb", "-s " + id + " shell stat -c %s " + Shell.Q(remote), 30000);
                        long rsz = 0; long.TryParse((stt.outp ?? "").Trim(), out rsz);
                        bool verified = push.ok && rsz == lsz && lsz > 0;
                        sb.AppendLine("   الجهاز: " + id);
                        sb.AppendLine("   دفع ملف اختبار: " + (push.ok ? "✔" : "✗ " + Trim(push.outp)));
                        sb.AppendLine("   التحقق بالحجم: " + (verified ? "✔ مطابق (" + rsz + " بايت)" : "✗ غير مطابق (محلي " + lsz + " / بعيد " + rsz + ")"));
                        var rm = Shell.Run("adb", "-s " + id + " shell rm -f " + Shell.Q(remote), 30000);
                        sb.AppendLine("   تنظيف ملف الاختبار: " + (rm.ok ? "✔" : "⚠"));
                        try { File.Delete(localTest); } catch { }
                        sb.AppendLine("   النتيجة: " + (verified ? "✔ النقل السريع والتحقق يعملان بشكل سليم" : "✗ مشكلة — راجع الكيبل/المنفذ/التصريح"));
                        if (!verified) LogError("فحص شامل", "فشل اختبار النقل السريع على " + id + " | " + Trim(push.outp));
                    }
                    catch (Exception ex) { sb.AppendLine("   ✗ خطأ بالاختبار: " + ex.Message); LogError("فحص شامل", ex.Message); }
                }
                sb.AppendLine("");

                // 4) معالجة الأخطاء
                sb.AppendLine("● معالجة الأخطاء (سجل الأخطاء):");
                try
                {
                    string el = Path.Combine(ExportsDir, "errors.log");
                    if (File.Exists(el))
                    {
                        var lines = File.ReadAllLines(el).Where(x => x.Trim() != "").ToList();
                        sb.AppendLine("   إجمالي الأخطاء المسجّلة: " + lines.Count);
                        sb.AppendLine("   آخر الأخطاء:");
                        foreach (var l in lines.AsEnumerable().Reverse().Take(5)) sb.AppendLine("      • " + l);
                    }
                    else sb.AppendLine("   ✔ لا أخطاء مسجّلة.");
                }
                catch { }
                sb.AppendLine("");
                sb.AppendLine("========================================");
                sb.AppendLine("سنتر بوينت للاتصالات");

                string path = null;
                try
                {
                    string exports = ExportsDir;
                    path = Path.Combine(exports, "CenterPoint_SelfTest_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
                    File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                }
                catch { }
                LogOp("فحص شامل");
                string fp = path;
                BeginInvoke(new Action(() =>
                {
                    SetText(monStatus, "✅ انتهى الفحص الشامل — التقرير محفوظ وفُتح.");
                    RepLoad();
                    if (fp != null) { try { System.Diagnostics.Process.Start(new ProcessStartInfo(fp) { UseShellExecute = true }); } catch { } }
                }));
            });
        }

        List<string> repFiles = new List<string>();

        void RepLoad()
        {
            repList.Items.Clear(); repFiles.Clear();
            string exports = ExportsDir;
            try
            {
                if (Directory.Exists(exports))
                {
                    var files = Directory.GetFiles(exports, "*.txt")
                        .Select(f => new FileInfo(f)).OrderByDescending(fi => fi.LastWriteTime).ToList();
                    foreach (var fi in files)
                    {
                        repFiles.Add(fi.FullName);
                        string kind = fi.Name.Contains("SelfTest") ? "🔍 فحص شامل" : fi.Name.Contains("Report") ? "📦 نقل" : fi.Name.Contains("Diagnostic") ? "🩺 تشخيص" : "📄";
                        repList.Items.Add(kind + "   " + fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + "   (" + FmtSize(fi.Length) + ")   " + fi.Name);
                    }
                    // سجل الأخطاء
                    string errLog = Path.Combine(exports, "errors.log");
                    if (File.Exists(errLog)) { var efi = new FileInfo(errLog); repFiles.Add(efi.FullName); repList.Items.Add("⚠ سجل الأخطاء   " + efi.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + "   (" + FmtSize(efi.Length) + ")   errors.log"); }
                }
            }
            catch { }
            if (repFiles.Count == 0) repList.Items.Add("لا توجد تقارير محفوظة بعد.");
            RepSummaryLoad();
        }

        void RepSummaryLoad()
        {
            string latest = repFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("Report"));
            if (latest == null) { repSummary.Text = "لا توجد تقارير نقل بعد. ابدأ نقلاً وسيظهر الملخّص هنا."; return; }
            try
            {
                var txt = File.ReadAllText(latest);
                string Get(string key) { var m = Regex.Match(txt, Regex.Escape(key) + @"\s*(.+)"); return m.Success ? m.Groups[1].Value.Trim() : "—"; }
                int errCount = 0;
                try { string el = Path.Combine(ExportsDir, "errors.log"); if (File.Exists(el)) errCount = File.ReadAllLines(el).Count(x => x.Trim() != ""); } catch { }
                repSummary.Text =
                    "الإجمالي: " + Get("الإجمالي:") + "    |    نجح: " + Get("نجح:") + "    |    فشل: " + Get("فشل:") + "\r\n" +
                    "الحجم: " + Get("الحجم الكلي:") + "    |    المدة: " + Get("المدة:") + "    |    متوسط السرعة: " + Get("متوسط السرعة:") +
                    (errCount > 0 ? "\r\n⚠ إجمالي الأخطاء المسجّلة: " + errCount + " (راجع errors.log)" : "");
            }
            catch { repSummary.Text = "تعذّر قراءة آخر تقرير."; }
        }

        void RepOpen()
        {
            int i = repList.SelectedIndex;
            if (i < 0 || i >= repFiles.Count) { MessageBox.Show("اختر تقريراً من القائمة."); return; }
            try { System.Diagnostics.Process.Start(new ProcessStartInfo(repFiles[i]) { UseShellExecute = true }); } catch (Exception ex) { ErrBox("تعذّر الفتح: " + ex.Message); }
        }

        void RepOpenFolder()
        {
            string exports = ExportsDir;
            try { System.Diagnostics.Process.Start(new ProcessStartInfo(exports) { UseShellExecute = true }); } catch { }
        }

        // ============ قسم MTP (بدون ADB) ============
        void BuildMtpPage()
        {
            pageMtp = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageMtp);

            var title = new Label { Text = "MTP - نقل الملفات بدون ADB", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(210, 10), Size = new Size(485, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageMtp.Controls.Add(title);
            Tr(title, "MTP - نقل الملفات بدون ADB", "MTP - File Transfer Without ADB");
            var compatBtn = new Button { Text = "🧪 فحص التوافق", Location = new Point(20, 11), Size = new Size(165, 27), BackColor = Color.FromArgb(130, 80, 190), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            compatBtn.FlatAppearance.BorderSize = 0; compatBtn.Click += (s, e) => CompatScan();
            pageMtp.Controls.Add(compatBtn); Tr(compatBtn, "🧪 فحص التوافق", "🧪 Compatibility");

            AddLbl(pageMtp, "جهاز المصدر:", 560, 44, 130);
            mtpSrcCb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(250, 44), Size = new Size(300, 26), Font = new Font("Arial", 10) };
            mtpSrcCb.SelectedIndexChanged += (s, e) => MtpOpenSource();
            pageMtp.Controls.Add(mtpSrcCb);
            mtpDetectBtn = new Button { Text = "🔄 اكتشاف الأجهزة", Location = new Point(40, 44), Size = new Size(200, 26), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            mtpDetectBtn.FlatAppearance.BorderSize = 0; mtpDetectBtn.Click += (s, e) => MtpDetect();
            pageMtp.Controls.Add(mtpDetectBtn);

            AddLbl(pageMtp, "جهاز الوجهة:", 560, 78, 130);
            mtpDstCb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(250, 78), Size = new Size(300, 26), Font = new Font("Arial", 10) };
            pageMtp.Controls.Add(mtpDstCb);
            mtpMoveBtn = new Button { Text = "▶ نقل المحدد للوجهة", Location = new Point(40, 78), Size = new Size(200, 26), BackColor = GREEN, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            mtpMoveBtn.FlatAppearance.BorderSize = 0; mtpMoveBtn.Click += (s, e) => MtpMove();
            pageMtp.Controls.Add(mtpMoveBtn);

            mtpUpBtn = new Button { Text = "⬆ رجوع", Location = new Point(600, 112), Size = new Size(95, 26), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9) };
            mtpUpBtn.FlatAppearance.BorderColor = Color.Gray; mtpUpBtn.Click += (s, e) => MtpUp();
            pageMtp.Controls.Add(mtpUpBtn);
            mtpPathLbl = new Label { Text = "المسار: —", ForeColor = Color.FromArgb(158, 203, 255), BackColor = BG, Location = new Point(20, 112), Size = new Size(570, 26), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9) };
            pageMtp.Controls.Add(mtpPathLbl);

            mtpList = new ListView { Location = new Point(20, 144), Size = new Size(675, 440), View = View.Details, CheckBoxes = true, FullRowSelect = true, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.White, Font = new Font("Arial", 10), BorderStyle = BorderStyle.None };
            mtpList.Columns.Add("الاسم", 520);
            mtpList.Columns.Add("النوع", 140);
            mtpList.DoubleClick += (s, e) => MtpOpenSelected();
            pageMtp.Controls.Add(mtpList);

            mtpStatusLbl = new Label { Text = "وصّل الموبايل بوضع «نقل الملفات (MTP)» واضغط «اكتشاف الأجهزة».", ForeColor = Color.FromArgb(201, 162, 39), BackColor = BG, Location = new Point(20, 592), Size = new Size(675, 50), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9) };
            pageMtp.Controls.Add(mtpStatusLbl);
        }

        void MtpStatus(string t) { if (mtpStatusLbl.InvokeRequired) mtpStatusLbl.BeginInvoke(new Action(() => mtpStatusLbl.Text = t)); else mtpStatusLbl.Text = t; }

        // ينفّذ عمل COM على خيط STA (مطلوب لـ Shell)
        void StaRun(Action work)
        {
            var th = new System.Threading.Thread(() => { try { work(); } catch (Exception ex) { MtpStatus("خطأ: " + ex.Message); } });
            th.SetApartmentState(System.Threading.ApartmentState.STA);
            th.IsBackground = true;
            th.Start();
        }

        static dynamic ShellApp() { return Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")); }

        void MtpDetect()
        {
            StaRun(() =>
            {
                var names = new List<string>();
                mtpDevices.Clear();
                dynamic sh = ShellApp();
                dynamic pc = sh.NameSpace(17); // This PC
                foreach (dynamic it in pc.Items())
                {
                    bool isFolder = false; string path = ""; string nm = "";
                    try { isFolder = it.IsFolder; path = (string)it.Path; nm = (string)it.Name; } catch { }
                    if (isFolder && path != null && path.StartsWith("::")) // جهاز محمول (MTP)
                    {
                        mtpDevices.Add(it); names.Add(nm);
                    }
                }
                void fill()
                {
                    mtpSrcCb.Items.Clear(); mtpDstCb.Items.Clear();
                    foreach (var n in names) { mtpSrcCb.Items.Add(n); mtpDstCb.Items.Add(n); }
                    if (names.Count >= 1) mtpSrcCb.SelectedIndex = 0;
                    if (names.Count >= 2) mtpDstCb.SelectedIndex = 1;
                    MtpStatus(names.Count + " جهاز MTP. " + (names.Count == 0 ? "تأكد أن الموبايل بوضع «نقل الملفات»." : "اختر المصدر واستعرض الملفات."));
                }
                if (mtpSrcCb.InvokeRequired) mtpSrcCb.BeginInvoke(new Action(fill)); else fill();
            });
        }

        // فاحص التوافق — يكتشف كل مسارات الاتصال ويبيّن المتاح والسبب عند التعذّر
        void CompatScan()
        {
            MtpStatus("جاري فحص التوافق لكل مسارات الاتصال...");
            StaRun(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("═══ فحص التوافق — Universal Transfer ═══");
                sb.AppendLine("(ملاحظة: هذا كشف للإمكانيات؛ الاختبار الفعلي يؤكّد الدعم لكل جهاز)");
                sb.AppendLine("");

                // 1) أندرويد — ADB (نقل كامل)
                int adbCount = 0;
                sb.AppendLine("● أندرويد عبر ADB  —  نقل كامل (صور/فيديو/ملفات + جهات اتصال + تطبيقات):");
                var ad = Shell.Run("adb", "devices", 30000);
                if (!ad.ok) sb.AppendLine("   ✗ ADB غير متاح على الكمبيوتر (ثبّته من صفحة USB).");
                else
                {
                    foreach (var ln in ad.outp.Replace("\r", "").Split('\n'))
                    {
                        var t = ln.Trim(); if (t == "" || t.StartsWith("List of")) continue;
                        var parts = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2) continue; adbCount++;
                        string id = parts[0], stt = parts[1];
                        if (stt == "device") sb.AppendLine("   ✔ " + id + " — متاح للنقل الكامل.");
                        else if (stt == "unauthorized") sb.AppendLine("   ⚠ " + id + " — غير مصرّح: افتح الهاتف واضغط «السماح بتصحيح USB».");
                        else if (stt == "offline") sb.AppendLine("   ⚠ " + id + " — offline: أعد توصيل الكيبل.");
                        else sb.AppendLine("   • " + id + " — " + stt);
                    }
                    if (adbCount == 0) sb.AppendLine("   ✗ لا جهاز عبر ADB — السبب: «تصحيح USB (USB Debugging)» غير مفعّل على الهاتف.");
                }
                sb.AppendLine("");

                // 2) أندرويد — MTP القياسي (بدون ADB)
                int mtpCount = 0;
                sb.AppendLine("● أندرويد عبر MTP القياسي  —  وسائط وملفات فقط (بدون جهات اتصال):");
                try
                {
                    dynamic sh = ShellApp(); dynamic pc = sh.NameSpace(17);
                    foreach (dynamic it in pc.Items())
                    {
                        bool isF = false; string path = "", nm = "";
                        try { isF = it.IsFolder; path = (string)it.Path; nm = (string)it.Name; } catch { }
                        if (isF && path != null && path.StartsWith("::")) { mtpCount++; sb.AppendLine("   ✔ " + nm + " — ظاهر لويندوز كجهاز MTP (نقل وسائط/ملفات متاح)."); }
                    }
                }
                catch (Exception ex) { sb.AppendLine("   (تعذّر فحص MTP: " + ex.Message + ")"); }
                if (mtpCount == 0) sb.AppendLine("   ✗ لا جهاز MTP ظاهر — السبب: الموبايل بوضع «شحن فقط» (غيّره إلى «نقل الملفات/MTP»)، أو كيبل شحن فقط، أو تعريف ناقص.");
                sb.AppendLine("");

                // 3) آيفون — مسار Apple المنفصل
                int appleCount = 0;
                sb.AppendLine("● آيفون/آيباد عبر تعريفات Apple  —  (iPhone لا يستخدم MTP القياسي):");
                var pmd = Shell.Run("pymobiledevice3", "usbmux list", 30000);
                if (pmd.ok)
                    foreach (Match m in Regex.Matches(pmd.outp, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{16}|[0-9a-f]{40}")) { appleCount++; sb.AppendLine("   ✔ آيفون (" + m.Value.Substring(0, Math.Min(8, m.Value.Length)) + "…) — متاح: وسائط عبر AFC، ونقل كامل عبر backup2."); break; }
                if (appleCount == 0)
                {
                    var idev = Shell.Run("idevice_id", "-l", 20000);
                    if (idev.ok && idev.outp.Trim().Length >= 20) { appleCount++; sb.AppendLine("   ✔ آيفون مكتشف عبر idevice_id — ثبّت iTunes/Apple Devices للنقل الكامل."); }
                }
                if (appleCount == 0) sb.AppendLine("   ✗ لا آيفون مكتشف — إن كان موصولاً: ثبّت iTunes/Apple Devices واضغط «الوثوق بهذا الكمبيوتر».");
                sb.AppendLine("");

                // الخلاصة
                sb.AppendLine("── الخلاصة ──");
                if (adbCount == 0 && mtpCount == 0 && appleCount == 0)
                    sb.AppendLine("⚠ لم يُكتشف أي مسار نقل. تحقّق من: وضع «نقل الملفات» على الهاتف، كيبل يدعم البيانات، منفذ USB آخر، والتعريفات.");
                else
                {
                    if (adbCount > 0) sb.AppendLine("✔ متاح: نقل أندرويد كامل عبر ADB.");
                    if (mtpCount > 0) sb.AppendLine("✔ متاح: نقل أندرويد (وسائط/ملفات) عبر MTP بدون ADB.");
                    if (appleCount > 0) sb.AppendLine("✔ متاح: آيفون عبر مسار Apple.");
                    sb.AppendLine("ℹ جهات الاتصال: عبر ADB (أندرويد) أو backup2 (آيفون) — غير متاحة عبر MTP القياسي.");
                }

                string report = sb.ToString();
                this.BeginInvoke(new Action(() =>
                {
                    MtpStatus("انتهى فحص التوافق.");
                    MessageBox.Show(report, "فحص التوافق — Universal Transfer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            });
        }

        void MtpOpenSource()
        {
            int idx = mtpSrcCb.SelectedIndex;
            if (idx < 0 || idx >= mtpDevices.Count) return;
            StaRun(() =>
            {
                dynamic dev = mtpDevices[idx];
                dynamic folder = dev.GetFolder;
                mtpNav.Clear();
                mtpCurrent = folder;
                MtpShow(folder, (string)dev.Name);
            });
        }

        void MtpShow(dynamic folder, string pathName)
        {
            var rows = new List<(string name, bool isFolder)>();
            foreach (dynamic it in folder.Items())
            {
                bool f = false; string nm = "";
                try { f = it.IsFolder; nm = (string)it.Name; } catch { }
                rows.Add((nm, f));
            }
            void fill()
            {
                mtpList.Items.Clear();
                foreach (var r in rows.OrderByDescending(x => x.isFolder))
                {
                    var lvi = new ListViewItem(r.name);
                    lvi.SubItems.Add(r.isFolder ? "مجلد" : "ملف");
                    lvi.ForeColor = r.isFolder ? Color.FromArgb(120, 190, 255) : Color.White;
                    mtpList.Items.Add(lvi);
                }
                mtpPathLbl.Text = "المسار: " + pathName;
                MtpStatus("دبل كلك على مجلد لفتحه. أشّر الملفات ثم «نقل المحدد للوجهة».");
            }
            if (mtpList.InvokeRequired) mtpList.BeginInvoke(new Action(fill)); else fill();
        }

        void MtpOpenSelected()
        {
            if (mtpList.SelectedItems.Count == 0) return;
            string name = mtpList.SelectedItems[0].Text;
            StaRun(() =>
            {
                dynamic cur = mtpCurrent;
                foreach (dynamic it in cur.Items())
                {
                    bool f = false; string nm = "";
                    try { f = it.IsFolder; nm = (string)it.Name; } catch { }
                    if (f && nm == name)
                    {
                        mtpNav.Push(cur);
                        dynamic sub = it.GetFolder;
                        mtpCurrent = sub;
                        MtpShow(sub, name);
                        return;
                    }
                }
            });
        }

        void MtpUp()
        {
            if (mtpNav.Count == 0) return;
            StaRun(() =>
            {
                dynamic prev = mtpNav.Pop();
                mtpCurrent = prev;
                string nm = ""; try { nm = (string)prev.Title; } catch { }
                MtpShow(prev, nm == "" ? "…" : nm);
            });
        }

        void MtpMove()
        {
            if (mtpSrcCb.SelectedIndex < 0 || mtpDstCb.SelectedIndex < 0) { MessageBox.Show("اختر المصدر والوجهة."); return; }
            if (mtpSrcCb.SelectedIndex == mtpDstCb.SelectedIndex) { MessageBox.Show("لازم جهازين مختلفين."); return; }
            var picked = new List<string>();
            foreach (ListViewItem it in mtpList.CheckedItems)
                if (it.SubItems.Count > 1 && it.SubItems[1].Text == "ملف") picked.Add(it.Text);
            if (picked.Count == 0) { MessageBox.Show("أشّر ملفاً واحداً على الأقل (الملفات مو المجلدات)."); return; }
            int dstIdx = mtpDstCb.SelectedIndex;
            if (!BeginOp("نقل MTP")) return;
            mtpMoveBtn.Enabled = false;
            StaRun(() =>
            {
                string tmp = Path.Combine(Path.GetTempPath(), "cp_mtp_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                Directory.CreateDirectory(tmp);
                dynamic sh = ShellApp();
                dynamic tempNs = sh.NameSpace(tmp);
                // وجهة على الجهاز الثاني: DCIM/Camera إن وُجد وإلا أول مخزن
                dynamic destFolder = MtpFindDestFolder(mtpDevices[dstIdx]);
                if (destFolder == null) { MtpStatus("تعذّر تحديد مجلد الوجهة."); EnableMove(); return; }

                int ok = 0, fail = 0, i = 0;
                dynamic cur = mtpCurrent;
                foreach (var fname in picked)
                {
                    i++;
                    MtpStatus("نقل (" + i + "/" + picked.Count + "): " + fname);
                    dynamic fileItem = null;
                    foreach (dynamic it in cur.Items()) { try { if (!it.IsFolder && (string)it.Name == fname) { fileItem = it; break; } } catch { } }
                    if (fileItem == null) { fail++; continue; }
                    // 1) نسخ من الهاتف للمجلد المؤقت
                    tempNs.CopyHere(fileItem, 0x214);
                    string localPath = MtpWaitFile(tmp, fname, 120);
                    if (localPath == null) { fail++; continue; }
                    // 2) نسخ من المؤقت للجهاز الثاني
                    dynamic localNs = sh.NameSpace(tmp);
                    dynamic localItem = localNs.ParseName(Path.GetFileName(localPath));
                    destFolder.CopyHere(localItem, 0x214);
                    // 3) التحقق: وجود الاسم + تطابق الحجم (اكتمال النسخ) قبل اعتباره ناجحاً
                    long expSz = 0; try { expSz = new FileInfo(localPath).Length; } catch { }
                    if (MtpDestHas(destFolder, fname, expSz, 180)) ok++;
                    else { fail++; MtpStatus("⚠ لم يكتمل نسخ: " + fname); LogError("MTP", fname + " | الحجم لم يتطابق/لم يصل"); }
                }
                MtpStatus("✅ انتهى: نجح (ومُتحقّق) " + ok + "، فشل " + fail + " من " + picked.Count);
                try { Directory.Delete(tmp, true); } catch { }
                EnableMove();
            });
        }

        void EnableMove() { EndOp(); if (mtpMoveBtn.InvokeRequired) mtpMoveBtn.BeginInvoke(new Action(() => mtpMoveBtn.Enabled = true)); else mtpMoveBtn.Enabled = true; }

        // التحقق من اكتمال نسخ الملف في الوجهة (MTP): وجود الاسم + تطابق الحجم.
        // للملفات الكبيرة: نواصل الانتظار ما دام الحجم يكبر (النسخ جارٍ)، ونستسلم فقط بعد توقّف التقدّم.
        bool MtpDestHas(dynamic folder, string name, long expectedSize, int seconds)
        {
            long lastSz = -1; int stall = 0; int maxStall = Math.Max(30, seconds * 2);
            while (stall < maxStall)
            {
                bool found = false; long sz = 0;
                try
                {
                    foreach (dynamic it in folder.Items())
                    {
                        try { if ((string)it.Name == name) { found = true; try { sz = (long)it.Size; } catch { } break; } }
                        catch { }
                    }
                }
                catch { }
                if (found)
                {
                    if (expectedSize <= 0) return true;        // حجم المصدر غير معروف — نكتفي بالوجود
                    if (sz == expectedSize) return true;       // اكتمل النسخ
                    if (sz > lastSz) { lastSz = sz; stall = 0; }   // الحجم يتقدّم — صفّر عدّاد التوقّف
                    else stall++;
                }
                else stall++;
                System.Threading.Thread.Sleep(500);
            }
            return false;
        }

        string MtpWaitFile(string dir, string name, int seconds)
        {
            for (int i = 0; i < seconds * 2; i++)
            {
                try { var p = Path.Combine(dir, name); if (File.Exists(p)) return p; } catch { }
                System.Threading.Thread.Sleep(500);
            }
            return null;
        }

        dynamic MtpFindDestFolder(object device)
        {
            try
            {
                dynamic dev = device;
                dynamic root = dev.GetFolder;
                // ادخل أول مخزن
                foreach (dynamic st in root.Items())
                {
                    if (!st.IsFolder) continue;
                    dynamic storage = st.GetFolder;
                    // ابحث عن DCIM
                    foreach (dynamic d in storage.Items())
                    {
                        try { if (d.IsFolder && (string)d.Name == "DCIM") return d.GetFolder; } catch { }
                    }
                    return storage; // وإلا جذر المخزن
                }
            }
            catch { }
            return null;
        }

        // ======================= المحرّك الموحّد (ADB/MTP/التحقق/التقارير) =======================
        // نُقل إلى MainForm.Engine.cs — نفس الكلاس الجزئي.

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // معالج أخطاء شامل — يسجّل أي استثناء ويمنع انهيار البرنامج
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                GlobalLogError("UI", e.Exception);
                try { MessageBox.Show("حدث خطأ غير متوقّع وتم تسجيله في errors.log — يمكنك المتابعة.\r\n\r\n" + e.Exception.Message, "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => GlobalLogError("Fatal", e.ExceptionObject as Exception);
            Application.Run(new MainForm());
        }

        // تسجيل خطأ شامل (ثابت — يُستدعى من Main قبل إنشاء النافذة)
        static void GlobalLogError(string where, Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CenterPoint Transfer", "exports");
                Directory.CreateDirectory(dir);
                string msg = ex == null ? "خطأ غير معروف" : (ex.Message + " | " + ex.StackTrace);
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | [خطأ شامل/" + where + "] | " + msg.Replace("\r", " ").Replace("\n", " ");
                File.AppendAllText(Path.Combine(dir, "errors.log"), line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }
}
