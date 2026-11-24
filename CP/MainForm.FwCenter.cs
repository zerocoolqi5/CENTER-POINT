// -*- coding: utf-8 -*-
// Center Point - مركز Firmware & Recovery — partial class
// كشف الجهاز + مطابقة الفيرموير + مكتبة + أدوات الشركات + Smart Recovery + سجل التفليش
// آمن: تشخيص وإرشاد وتسجيل — لا تفليش داخلي، ولا تجاوز FRP.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using System.Text.Json;

namespace CenterPoint
{
    public class FwEntry
    {
        public string Model { get; set; }
        public string Android { get; set; }
        public string Chipset { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public string Md5 { get; set; }
        public string Note { get; set; }
        public string Added { get; set; }
    }

    public class FwVendor
    {
        public string Name { get; set; }
        public string Key { get; set; }
        public string Path { get; set; }
    }

    public class FlashOp
    {
        public string Date { get; set; }
        public string Model { get; set; }
        public string Tool { get; set; }
        public string Firmware { get; set; }
        public string Result { get; set; }
        public string Note { get; set; }
    }

    public partial class MainForm
    {
        Panel pageFw; Button biFw;
        Panel fwDetP, fwMgrP, fwLibP, fwToolP, fwRecP, fwLogP;
        Button fwTabDet, fwTabMgr, fwTabLib, fwTabTool, fwTabRec, fwTabLog;
        // كشف ومطابقة
        TextBox fwDetRes; string fwDetModel = "", fwDetChip = "", fwCompFile = "";
        // Manager
        TextBox fwMgrRes, fwMgrModel, fwMgrAndroid; string fwMgrFile = "";
        // المكتبة
        TextBox fwLibModel, fwLibAndroid, fwLibChip, fwLibNote, fwLibSearch; ListBox fwLibList; string fwLibPath = "";
        List<FwEntry> fwLib = new List<FwEntry>();
        // أدوات الشركات
        ListBox fwToolList; TextBox fwToolRes; List<FwVendor> fwTools = new List<FwVendor>();
        // Recovery
        TextBox fwRecRes;
        // سجل التفليش
        TextBox fwLogModelB, fwLogFwB, fwLogNoteB; ComboBox fwLogToolC, fwLogResultC; ListBox fwLogList;
        List<FlashOp> flashLog = new List<FlashOp>();

        string FwLibJson { get { return System.IO.Path.Combine(ExportsDir, "firmware_lib.json"); } }
        string FwToolsJson { get { return System.IO.Path.Combine(ExportsDir, "vendor_tools.json"); } }
        string FlashLogJson { get { return System.IO.Path.Combine(ExportsDir, "flash_log.json"); } }

        void BuildFwPage()
        {
            pageFw = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageFw);

            var title = new Label { Text = "🧰 Firmware & Recovery", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 10), Size = new Size(675, 26), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageFw.Controls.Add(title);
            Tr(title, "🧰 Firmware & Recovery", "🧰 Firmware & Recovery");

            // صف التبويبات
            fwTabDet = FwTabBtn("🔍 كشف", 20);
            fwTabDet.Click += (s, e) => FwTab("det");
            fwTabMgr = FwTabBtn("📂 Manager", 133);
            fwTabMgr.Click += (s, e) => FwTab("mgr");
            fwTabLib = FwTabBtn("📚 المكتبة", 246);
            fwTabLib.Click += (s, e) => FwTab("lib");
            fwTabTool = FwTabBtn("🧰 الأدوات", 359);
            fwTabTool.Click += (s, e) => FwTab("tool");
            fwTabRec = FwTabBtn("🩺 Recovery", 472);
            fwTabRec.Click += (s, e) => FwTab("rec");
            fwTabLog = FwTabBtn("📜 السجل", 585);
            fwTabLog.Click += (s, e) => FwTab("log");

            BuildFwDetect();
            BuildFwMgr();
            BuildFwLib();
            BuildFwTools();
            BuildFwRecovery();
            BuildFwLog();

            FwToolsLoad(); FwLibLoad(); FlashLogLoad();
            FwTab("det");
        }

        Button FwTabBtn(string text, int x)
        {
            var b = new Button { Text = text, Location = new Point(x, 44), Size = new Size(108, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            b.FlatAppearance.BorderColor = BLUE;
            pageFw.Controls.Add(b);
            return b;
        }

        void FwTab(string w)
        {
            if (fwDetP != null) fwDetP.Visible = (w == "det");
            if (fwMgrP != null) fwMgrP.Visible = (w == "mgr");
            if (fwLibP != null) fwLibP.Visible = (w == "lib");
            if (fwToolP != null) fwToolP.Visible = (w == "tool");
            if (fwRecP != null) fwRecP.Visible = (w == "rec");
            if (fwLogP != null) fwLogP.Visible = (w == "log");
            FwTabColor(fwTabDet, w == "det"); FwTabColor(fwTabMgr, w == "mgr"); FwTabColor(fwTabLib, w == "lib");
            FwTabColor(fwTabTool, w == "tool"); FwTabColor(fwTabRec, w == "rec"); FwTabColor(fwTabLog, w == "log");
        }

        void FwTabColor(Button b, bool on)
        {
            if (b == null) return;
            b.BackColor = on ? BLUE : CARD;
        }

        Panel FwSub()
        {
            var p = new Panel { Location = new Point(0, 82), Size = new Size(715, 578), BackColor = BG, Visible = false };
            pageFw.Controls.Add(p);
            return p;
        }

        TextBox FwLog(Panel p, int y, int h)
        {
            var t = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(255, 200, 120), Location = new Point(20, y), Size = new Size(675, h), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            p.Controls.Add(t); return t;
        }

        TextBox FwBox(Panel p, int x, int y, int w)
        {
            var t = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 10) };
            p.Controls.Add(t); return t;
        }

        void FwLbl(Panel p, string text, int x, int y)
        {
            p.Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(90, 20), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold) });
        }

        Button FwBtn(Panel p, string text, int x, int y, int w, Color col)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 30), BackColor = col, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0;
            p.Controls.Add(b); return b;
        }

        // ============ تبويب 1: كشف ومطابقة ============
        void BuildFwDetect()
        {
            fwDetP = FwSub();
            var b1 = FwBtn(fwDetP, "🔍 كشف الجهاز", 20, 10, 210, BLUE);
            b1.Click += (s, e) => FwDetect();
            var b2 = FwBtn(fwDetP, "📂 اختر ملف فيرموير", 238, 10, 230, CARD);
            b2.BackColor = CARD; b2.FlatAppearance.BorderColor = BLUE; b2.FlatAppearance.BorderSize = 1;
            b2.Click += (s, e) => FwPickCompFile();
            var b3 = FwBtn(fwDetP, "✔ تحقّق التوافق", 476, 10, 219, Color.FromArgb(35, 115, 75));
            b3.Click += (s, e) => FwCheckCompat();
            fwDetRes = FwLog(fwDetP, 52, 514);
            fwDetRes.Text = "اضغط «كشف الجهاز» لقراءة الشركة والموديل والمعالج ونظام أندرويد،\r\nثم «اختر ملف فيرموير» و«تحقّق التوافق» لمطابقته مع الجهاز قبل التفليش.";
        }

        void FwDetect()
        {
            SetTb(fwDetRes, "جاري الكشف...");
            Task.Run(() =>
            {
                var id = DeviceDetect();
                fwDetModel = id.Model; fwDetChip = id.ChipFamily;
                var sb = new StringBuilder();
                sb.AppendLine("═══ كشف الجهاز (موحّد) ═══");
                sb.AppendLine("");
                sb.AppendLine("● الحالة:   " + id.StateArabic() + (id.Serial != "" ? "  [" + id.Serial + "]" : ""));
                if (id.State == "system")
                {
                    sb.AppendLine("● الشركة:   " + id.Mfr);
                    sb.AppendLine("● الموديل:  " + id.Model + "   (" + id.Device + ")");
                    sb.AppendLine("● المعالج:  " + id.Platform + " / " + id.Hardware + "  ←  عائلة: " + FwChipArabic(id.ChipFamily));
                    sb.AppendLine("● أندرويد:  " + id.Android + " (SDK " + id.Sdk + ")");
                    sb.AppendLine("● البناء:   " + id.Build);
                    sb.AppendLine("");
                    sb.AppendLine("● الأداة المقترحة: " + FwToolForChip(id.ChipFamily));
                    sb.AppendLine("");
                    sb.AppendLine("خذ هذه البيانات للبحث عن الفيرموير المطابق تماماً. ثم «اختر ملف فيرموير» و«تحقّق التوافق».");
                }
                else if (id.State == "fastboot")
                {
                    sb.AppendLine("● الموديل (fastboot): " + (id.Model != "" ? id.Model : "غير معروف"));
                    sb.AppendLine("● المعالج المقترح: " + FwChipArabic(id.ChipFamily));
                    sb.AppendLine("");
                    sb.AppendLine("الجهاز بوضع Fastboot — استخدم مركز التفليش «fastboot flash».");
                }
                else
                {
                    sb.AppendLine("");
                    sb.AppendLine("✖ لا يوجد جهاز ظاهر بـADB/Fastboot.");
                    sb.AppendLine("• وصّل الهاتف وفعّل «تصحيح USB».");
                    sb.AppendLine("• بوضع CrashDump/Download/EDL لن يظهر — خذ الموديل من الملصق واذهب لمركز التفليش.");
                }
                CpLog("OP", "كشف جهاز: " + (id.Present ? (id.Model + " / " + id.StateArabic()) : "لا جهاز"));
                SetTb(fwDetRes, sb.ToString());
            });
        }

        void FwPickCompFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "اختر ملف فيرموير للتحقق";
                dlg.Filter = "ملفات الفيرموير|*.zip;*.tar;*.md5;*.pac;*.img;*.ozip;*.ofp;*.kdz;*.tot;*.ipsw;*.bin|كل الملفات|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                fwCompFile = dlg.FileName;
            }
            long sz = 0; try { sz = new FileInfo(fwCompFile).Length; } catch { }
            SetTb(fwDetRes, "الملف المختار: " + System.IO.Path.GetFileName(fwCompFile) + "   (" + FwSizeStr(sz) + ")\r\nنوع الملف: " + FwFileType(fwCompFile) + "\r\n\r\nاضغط «تحقّق التوافق».");
        }

        void FwCheckCompat()
        {
            if (fwCompFile == "" || !File.Exists(fwCompFile)) { MessageBox.Show("اختر ملف فيرموير أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var sb = new StringBuilder();
            sb.AppendLine("═══ تحقّق التوافق (مبدئي) ═══");
            sb.AppendLine("");
            string fname = System.IO.Path.GetFileName(fwCompFile);
            string ftype = FwFileType(fwCompFile);
            string fchipByExt = FwChipByExt(fwCompFile);
            sb.AppendLine("● الملف: " + fname);
            sb.AppendLine("● النوع: " + ftype);
            sb.AppendLine("");
            if (fwDetModel == "") sb.AppendLine("⚠ لم يُكشف الجهاز بعد — اضغط «كشف الجهاز» لمطابقة أدق.");
            else
            {
                string mTok = FwModelToken(fwDetModel);
                bool nameMatch = mTok != "" && fname.ToUpperInvariant().Contains(mTok);
                sb.AppendLine("● موديل الجهاز: " + fwDetModel);
                sb.AppendLine("● تطابق الاسم: " + (nameMatch ? "✔ اسم الملف يحوي موديل الجهاز" : "⚠ اسم الملف لا يحوي الموديل — تأكد يدوياً"));
                sb.AppendLine("● معالج الجهاز: " + FwChipArabic(fwDetChip));
                if (fchipByExt != "" && fwDetChip != "" && fchipByExt != "any")
                    sb.AppendLine("● توافق المعالج: " + (fchipByExt == fwDetChip ? "✔ صيغة الملف تناسب المعالج" : "⚠ صيغة الملف (" + FwChipArabic(fchipByExt) + ") قد لا تناسب معالج الجهاز"));
            }
            sb.AppendLine("");
            sb.AppendLine("⛔ هذا فحص مبدئي بالاسم والصيغة فقط — لا يضمن التطابق. التفليش بفيرموير غير مطابق = بريك.");
            sb.AppendLine("تأكد أن الفيرموير لنفس الموديل والمنطقة (CSC) بالضبط قبل التفليش.");
            SetTb(fwDetRes, sb.ToString());
        }

        // ============ تبويب 2: Firmware Manager ============
        void BuildFwMgr()
        {
            fwMgrP = FwSub();
            var b1 = FwBtn(fwMgrP, "📂 اختر ملف", 20, 10, 190, BLUE);
            b1.Click += (s, e) => FwMgrPick();
            var b2 = FwBtn(fwMgrP, "🔐 احسب MD5", 218, 10, 180, CARD);
            b2.FlatAppearance.BorderColor = BLUE; b2.FlatAppearance.BorderSize = 1;
            b2.Click += (s, e) => FwMgrHash();
            FwLbl(fwMgrP, "موديل:", 406, 14);
            fwMgrModel = FwBox(fwMgrP, 20, 50, 190); fwMgrModel.Location = new Point(500, 12); fwMgrModel.Size = new Size(195, 28);
            FwLbl(fwMgrP, "أندرويد:", 406, 50);
            fwMgrAndroid = FwBox(fwMgrP, 500, 48, 195);
            var b3 = FwBtn(fwMgrP, "➕ أضف للمكتبة", 20, 86, 230, Color.FromArgb(35, 115, 75));
            b3.Click += (s, e) => FwMgrAddToLib();
            fwMgrRes = FwLog(fwMgrP, 128, 438);
            fwMgrRes.Text = "اختر ملف فيرموير لعرض نوعه وحجمه وبصمته (MD5)،\r\nثم اكتب الموديل ونظام أندرويد و«أضف للمكتبة» لأرشفته.";
        }

        void FwMgrPick()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "اختر ملف فيرموير";
                dlg.Filter = "ملفات الفيرموير|*.zip;*.tar;*.md5;*.pac;*.img;*.ozip;*.ofp;*.kdz;*.tot;*.ipsw;*.bin|كل الملفات|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                fwMgrFile = dlg.FileName;
            }
            long sz = 0; try { sz = new FileInfo(fwMgrFile).Length; } catch { }
            var sb = new StringBuilder();
            sb.AppendLine("═══ ملف الفيرموير ═══");
            sb.AppendLine("");
            sb.AppendLine("● الاسم: " + System.IO.Path.GetFileName(fwMgrFile));
            sb.AppendLine("● النوع: " + FwFileType(fwMgrFile));
            sb.AppendLine("● الحجم: " + FwSizeStr(sz));
            sb.AppendLine("● المسار: " + fwMgrFile);
            sb.AppendLine("");
            sb.AppendLine("اضغط «احسب MD5» للبصمة (قد يأخذ وقتاً للملفات الكبيرة).");
            SetTb(fwMgrRes, sb.ToString());
        }

        void FwMgrHash()
        {
            if (fwMgrFile == "" || !File.Exists(fwMgrFile)) { MessageBox.Show("اختر ملفاً أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            SetTb(fwMgrRes, "جاري حساب MD5... لا تغلق البرنامج (قد يأخذ دقائق للملفات الكبيرة).");
            Task.Run(() =>
            {
                string md5 = FwMd5(fwMgrFile);
                long sz = 0; try { sz = new FileInfo(fwMgrFile).Length; } catch { }
                var sb = new StringBuilder();
                sb.AppendLine("═══ بصمة الملف ═══");
                sb.AppendLine("");
                sb.AppendLine("● الاسم: " + System.IO.Path.GetFileName(fwMgrFile));
                sb.AppendLine("● الحجم: " + FwSizeStr(sz));
                sb.AppendLine("● MD5:  " + md5);
                sb.AppendLine("");
                sb.AppendLine("قارن البصمة مع المصدر للتأكد أن التحميل سليم غير تالف.");
                SetTb(fwMgrRes, sb.ToString());
            });
        }

        void FwMgrAddToLib()
        {
            if (fwMgrFile == "" || !File.Exists(fwMgrFile)) { MessageBox.Show("اختر ملفاً أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            long sz = 0; try { sz = new FileInfo(fwMgrFile).Length; } catch { }
            var e = new FwEntry
            {
                Model = fwMgrModel.Text.Trim(),
                Android = fwMgrAndroid.Text.Trim(),
                Chipset = FwChipByExt(fwMgrFile),
                Path = fwMgrFile,
                Size = sz,
                Md5 = "",
                Note = FwFileType(fwMgrFile),
                Added = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
            fwLib.Insert(0, e); FwLibSave(); FwLibRefresh();
            MessageBox.Show("أُضيف للمكتبة ✔", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LogOp("فيرموير بالمكتبة: " + e.Model);
        }

        // ============ تبويب 3: المكتبة ============
        void BuildFwLib()
        {
            fwLibP = FwSub();
            FwLbl(fwLibP, "موديل:", 20, 14);
            fwLibModel = FwBox(fwLibP, 85, 12, 190);
            FwLbl(fwLibP, "أندرويد:", 285, 14);
            fwLibAndroid = FwBox(fwLibP, 360, 12, 100);
            FwLbl(fwLibP, "معالج:", 470, 14);
            fwLibChip = FwBox(fwLibP, 535, 12, 160);
            FwLbl(fwLibP, "ملاحظة:", 20, 50);
            fwLibNote = FwBox(fwLibP, 85, 48, 375);
            var pick = FwBtn(fwLibP, "📂 ملف", 470, 46, 110, CARD);
            pick.FlatAppearance.BorderColor = BLUE; pick.FlatAppearance.BorderSize = 1;
            pick.Click += (s, e) => { using (var d = new OpenFileDialog()) { if (d.ShowDialog(this) == DialogResult.OK) { fwLibPath = d.FileName; MessageBox.Show("مسار: " + System.IO.Path.GetFileName(fwLibPath)); } } };
            var add = FwBtn(fwLibP, "➕ حفظ", 585, 46, 110, Color.FromArgb(35, 115, 75));
            add.Click += (s, e) => FwLibAdd();

            FwLbl(fwLibP, "بحث:", 20, 90);
            fwLibSearch = FwBox(fwLibP, 85, 88, 375);
            fwLibSearch.TextChanged += (s, e) => FwLibRefresh();
            var del = FwBtn(fwLibP, "🗑 حذف", 470, 86, 110, Color.FromArgb(150, 60, 60));
            del.Click += (s, e) => FwLibDel();
            var openf = FwBtn(fwLibP, "📁 فتح مكان", 585, 86, 110, CARD);
            openf.FlatAppearance.BorderColor = BLUE; openf.FlatAppearance.BorderSize = 1;
            openf.Click += (s, e) => FwLibOpen();

            fwLibList = new ListBox { Location = new Point(20, 128), Size = new Size(675, 438), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            fwLibP.Controls.Add(fwLibList);
        }

        void FwLibAdd()
        {
            string model = fwLibModel.Text.Trim();
            if (model == "" && fwLibPath == "") { MessageBox.Show("اكتب الموديل أو اختر ملفاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            long sz = 0; try { if (fwLibPath != "") sz = new FileInfo(fwLibPath).Length; } catch { }
            fwLib.Insert(0, new FwEntry { Model = model, Android = fwLibAndroid.Text.Trim(), Chipset = fwLibChip.Text.Trim(), Path = fwLibPath, Size = sz, Md5 = "", Note = fwLibNote.Text.Trim(), Added = DateTime.Now.ToString("yyyy-MM-dd HH:mm") });
            FwLibSave(); FwLibRefresh();
            fwLibModel.Text = ""; fwLibAndroid.Text = ""; fwLibChip.Text = ""; fwLibNote.Text = ""; fwLibPath = "";
            LogOp("فيرموير بالمكتبة: " + model);
        }

        void FwLibDel()
        {
            int i = fwLibList.SelectedIndex;
            var shown = FwLibShown();
            if (i < 0 || i >= shown.Count) { MessageBox.Show("اختر عنصراً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var target = shown[i];
            if (MessageBox.Show("حذف: " + target.Model + "؟", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            fwLib.Remove(target); FwLibSave(); FwLibRefresh();
        }

        void FwLibOpen()
        {
            int i = fwLibList.SelectedIndex;
            var shown = FwLibShown();
            if (i < 0 || i >= shown.Count) return;
            string p = shown[i].Path;
            if (string.IsNullOrEmpty(p)) { MessageBox.Show("لا يوجد مسار محفوظ لهذا العنصر."); return; }
            try { if (File.Exists(p)) Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "/select,\"" + p + "\"", UseShellExecute = true }); else MessageBox.Show("الملف لم يعد موجوداً:\r\n" + p); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        List<FwEntry> FwLibShown()
        {
            string q = (fwLibSearch != null ? fwLibSearch.Text : "").Trim().ToLowerInvariant();
            if (q == "") return fwLib.ToList();
            return fwLib.Where(x => ((x.Model ?? "") + " " + (x.Android ?? "") + " " + (x.Chipset ?? "") + " " + (x.Note ?? "")).ToLowerInvariant().Contains(q)).ToList();
        }

        void FwLibRefresh()
        {
            if (fwLibList == null) return;
            fwLibList.Items.Clear();
            foreach (var e in FwLibShown())
                fwLibList.Items.Add((e.Model ?? "") + "  |  " + (e.Android ?? "-") + "  |  " + (e.Chipset ?? "-") + "  |  " + FwSizeStr(e.Size) + "  |  " + (e.Note ?? "") + (string.IsNullOrEmpty(e.Path) ? "" : "  📂"));
        }

        void FwLibLoad()
        {
            try { if (File.Exists(FwLibJson)) fwLib = JsonSerializer.Deserialize<List<FwEntry>>(File.ReadAllText(FwLibJson)) ?? new List<FwEntry>(); }
            catch { fwLib = new List<FwEntry>(); }
            FwLibRefresh();
        }

        void FwLibSave()
        {
            try { File.WriteAllText(FwLibJson, JsonSerializer.Serialize(fwLib, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(true)); }
            catch (Exception ex) { ErrBox("تعذّر حفظ المكتبة: " + ex.Message); }
        }

        // ============ تبويب 4: أدوات الشركات ============
        void BuildFwTools()
        {
            fwToolP = FwSub();
            var note = new Label { Text = "أدوات التفليش الرسمية — عيّن مسار كل أداة لتشغيلها بسرعة، أو افتح صفحة تنزيلها.", ForeColor = Color.White, BackColor = Color.FromArgb(30, 60, 110), Location = new Point(20, 10), Size = new Size(675, 30), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9), Padding = new Padding(8, 0, 8, 0) };
            fwToolP.Controls.Add(note);

            fwToolList = new ListBox { Location = new Point(20, 50), Size = new Size(675, 300), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            fwToolP.Controls.Add(fwToolList);

            var setp = FwBtn(fwToolP, "📂 تعيين المسار", 20, 358, 165, BLUE);
            setp.Click += (s, e) => FwToolSetPath();
            var run = FwBtn(fwToolP, "🚀 تشغيل", 193, 358, 160, Color.FromArgb(150, 90, 20));
            run.Click += (s, e) => FwToolRun();
            var dl = FwBtn(fwToolP, "⬇ صفحة التنزيل", 361, 358, 165, Color.FromArgb(35, 115, 75));
            dl.Click += (s, e) => FwToolDownload();
            var clr = FwBtn(fwToolP, "✖ مسح المسار", 534, 358, 161, CARD);
            clr.FlatAppearance.BorderColor = BLUE; clr.FlatAppearance.BorderSize = 1;
            clr.Click += (s, e) => FwToolClear();

            fwToolRes = FwLog(fwToolP, 400, 166);
            fwToolRes.Text = "اختر أداة من القائمة ثم «تعيين المسار» (ملف .exe) لتشغيلها لاحقاً بضغطة.\r\n«صفحة التنزيل» تفتح بحثاً عن النسخة الرسمية.";
        }

        void FwToolsSeed()
        {
            string[,] seed = {
                { "Odin (Samsung)", "odin" },
                { "SP Flash Tool (MediaTek)", "spft" },
                { "QFIL / QPST (Qualcomm)", "qfil" },
                { "ResearchDownload (Unisoc/SPD)", "spd" },
                { "RKDevTool (Rockchip)", "rk" },
                { "PhoenixSuit / LiveSuit (Allwinner)", "phoenix" },
                { "Mi Flash (Xiaomi)", "miflash" },
                { "LGUP (LG)", "lgup" },
                { "iTunes / 3uTools (Apple)", "itunes" },
            };
            for (int i = 0; i < seed.GetLength(0); i++)
                if (!fwTools.Any(t => t.Key == seed[i, 1]))
                    fwTools.Add(new FwVendor { Name = seed[i, 0], Key = seed[i, 1], Path = "" });
        }

        void FwToolsLoad()
        {
            try { if (File.Exists(FwToolsJson)) fwTools = JsonSerializer.Deserialize<List<FwVendor>>(File.ReadAllText(FwToolsJson)) ?? new List<FwVendor>(); }
            catch { fwTools = new List<FwVendor>(); }
            FwToolsSeed();
            FwToolsRefresh();
        }

        void FwToolsSave()
        {
            try { File.WriteAllText(FwToolsJson, JsonSerializer.Serialize(fwTools, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(true)); }
            catch { }
        }

        void FwToolsRefresh()
        {
            if (fwToolList == null) return;
            int sel = fwToolList.SelectedIndex;
            fwToolList.Items.Clear();
            foreach (var t in fwTools)
                fwToolList.Items.Add((t.Name ?? "") + "   " + (string.IsNullOrEmpty(t.Path) ? "—  (لم يُعيّن)" : "✔ " + System.IO.Path.GetFileName(t.Path)));
            if (sel >= 0 && sel < fwToolList.Items.Count) fwToolList.SelectedIndex = sel;
        }

        void FwToolSetPath()
        {
            int i = fwToolList.SelectedIndex;
            if (i < 0 || i >= fwTools.Count) { MessageBox.Show("اختر أداة أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "اختر ملف " + fwTools[i].Name;
                dlg.Filter = "برامج|*.exe|كل الملفات|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                fwTools[i].Path = dlg.FileName;
            }
            FwToolsSave(); FwToolsRefresh();
        }

        void FwToolRun()
        {
            int i = fwToolList.SelectedIndex;
            if (i < 0 || i >= fwTools.Count) { MessageBox.Show("اختر أداة أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string p = fwTools[i].Path;
            if (string.IsNullOrEmpty(p) || !File.Exists(p)) { MessageBox.Show("لم يُعيّن مسار صحيح لهذه الأداة. استخدم «تعيين المسار».", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try { Process.Start(new ProcessStartInfo { FileName = p, UseShellExecute = true }); LogOp("تشغيل أداة: " + fwTools[i].Name); }
            catch (Exception ex) { ErrBox("تعذّر التشغيل: " + ex.Message); }
        }

        void FwToolDownload()
        {
            int i = fwToolList.SelectedIndex;
            if (i < 0 || i >= fwTools.Count) { MessageBox.Show("اختر أداة أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string q = Uri.EscapeDataString(fwTools[i].Name + " official download");
            try { Process.Start(new ProcessStartInfo { FileName = "https://www.google.com/search?q=" + q, UseShellExecute = true }); } catch { }
        }

        void FwToolClear()
        {
            int i = fwToolList.SelectedIndex;
            if (i < 0 || i >= fwTools.Count) return;
            fwTools[i].Path = ""; FwToolsSave(); FwToolsRefresh();
        }

        // ============ تبويب 5: Smart Recovery ============
        void BuildFwRecovery()
        {
            fwRecP = FwSub();
            var b1 = FwBtn(fwRecP, "🩺 تشخيص الحالة", 20, 10, 220, BLUE);
            b1.Click += (s, e) => FwRecDiag();
            var b2 = FwBtn(fwRecP, "↻ إعادة لـRecovery", 248, 10, 220, CARD);
            b2.FlatAppearance.BorderColor = BLUE; b2.FlatAppearance.BorderSize = 1;
            b2.Click += (s, e) => FwRecReboot("recovery");
            var b3 = FwBtn(fwRecP, "⚡ إعادة لـBootloader", 476, 10, 219, CARD);
            b3.FlatAppearance.BorderColor = BLUE; b3.FlatAppearance.BorderSize = 1;
            b3.Click += (s, e) => FwRecReboot("bootloader");
            fwRecRes = FwLog(fwRecP, 52, 514);
            fwRecRes.Text = "Smart Recovery — تشخيص آمن (لا يمسح بيانات):\r\n\r\nاضغط «تشخيص الحالة» لمعرفة وضع الجهاز (طبيعي / Recovery / Fastboot / CrashDump) والخطوة الموصى بها.\r\n\r\nأزرار الإعادة آمنة وتعمل فقط إن كان الجهاز متصلاً بـADB/Fastboot.";
        }

        void FwRecDiag()
        {
            SetTb(fwRecRes, "جاري التشخيص...");
            Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("═══ Smart Recovery — تشخيص ═══");
                sb.AppendLine("");
                string adbDev = Shell.Run("adb", "devices", 15000).outp ?? "";
                string fbDev = Shell.Run("fastboot", "devices", 12000).outp ?? "";
                bool hasAdb = adbDev.Split('\n').Count(l => l.Trim().EndsWith("device")) > 0;
                bool hasRecovery = adbDev.ToLowerInvariant().Contains("recovery");
                bool hasSideload = adbDev.ToLowerInvariant().Contains("sideload");
                bool hasFastboot = (fbDev ?? "").Trim().Length > 0 && fbDev.ToLowerInvariant().Contains("fastboot");

                if (hasFastboot)
                {
                    sb.AppendLine("● الحالة: Fastboot / Bootloader ⚡");
                    sb.AppendLine("● موصى: من مركز التفليش «fastboot flash» للأقسام، أو «fastboot reboot» للإقلاع.");
                }
                else if (hasSideload)
                {
                    sb.AppendLine("● الحالة: Recovery — Sideload 📥");
                    sb.AppendLine("● موصى: قسم السوفت وير ← «تطبيق OTA» (adb sideload) بملف .zip رسمي.");
                }
                else if (hasRecovery)
                {
                    sb.AppendLine("● الحالة: Recovery 🔧");
                    sb.AppendLine("● موصى: Wipe cache (آمن) ← Reboot. لإعادة تثبيت النظام استخدم sideload بملف رسمي.");
                }
                else if (hasAdb)
                {
                    var d = devices.FirstOrDefault(x => x.Kind == "android");
                    string model = d != null ? Devices.Prop(d, "ro.product.model") : "";
                    sb.AppendLine("● الحالة: النظام يعمل طبيعي ✔  " + (model != "" ? "(" + model + ")" : ""));
                    sb.AppendLine("● موصى: نسخة احتياطية أولاً. إن كان فيه بطء/تعليق جرّب Wipe cache من Recovery.");
                }
                else
                {
                    sb.AppendLine("● الحالة: لا جهاز ظاهر بـADB/Fastboot.");
                    sb.AppendLine("● الاحتمالات والخطوات:");
                    sb.AppendLine("   - CrashDump (Unisoc): شاشة حمراء نص أبيض ← مركز التفليش (Unisoc/SPD) بملف .pac.");
                    sb.AppendLine("   - Download (Samsung): ← مركز التفليش Samsung (Odin).");
                    sb.AppendLine("   - EDL 9008 (Qualcomm): ← QFIL بملفات rawprogram.");
                    sb.AppendLine("   - لا شيء: تأكد من الكيبل/التعريفات وتفعيل «تصحيح USB».");
                }
                sb.AppendLine("");
                sb.AppendLine("ℹ تشخيص فقط — لم يُعدّل أو يُمسح أي شيء. لا نتجاوز أي قفل حساب (FRP).");
                SetTb(fwRecRes, sb.ToString());
            });
        }

        void FwRecReboot(string mode)
        {
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "reboot " + mode, 20000);
                string extra = "";
                if (!r.ok || (r.outp ?? "").ToLowerInvariant().Contains("no devices"))
                    extra = "\r\n(لم ينجح عبر adb — إن كان بوضع Fastboot جرّب الزر الآخر أو افصل ووصّل.)";
                SetTb(fwRecRes, "أمر الإعادة إلى " + mode + ":\r\n" + (r.outp ?? "") + extra);
                LogOp("إعادة تشغيل: " + mode);
            });
        }

        // ============ تبويب 6: سجل التفليش ============
        void BuildFwLog()
        {
            fwLogP = FwSub();
            FwLbl(fwLogP, "موديل:", 20, 14);
            fwLogModelB = FwBox(fwLogP, 85, 12, 190);
            FwLbl(fwLogP, "الأداة:", 285, 14);
            fwLogToolC = new ComboBox { Location = new Point(350, 12), Size = new Size(150, 28), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, Font = new Font("Arial", 9) };
            fwLogToolC.Items.AddRange(new object[] { "Odin", "SP Flash Tool", "QFIL", "ResearchDownload", "RKDevTool", "PhoenixSuit", "Mi Flash", "LGUP", "iTunes", "fastboot", "sideload", "أخرى" });
            fwLogToolC.SelectedIndex = 0; fwLogP.Controls.Add(fwLogToolC);
            FwLbl(fwLogP, "النتيجة:", 510, 14);
            fwLogResultC = new ComboBox { Location = new Point(575, 12), Size = new Size(120, 28), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, Font = new Font("Arial", 9) };
            fwLogResultC.Items.AddRange(new object[] { "نجح ✔", "فشل ✖", "ملغى" });
            fwLogResultC.SelectedIndex = 0; fwLogP.Controls.Add(fwLogResultC);

            FwLbl(fwLogP, "فيرموير:", 20, 50);
            fwLogFwB = FwBox(fwLogP, 85, 48, 300);
            FwLbl(fwLogP, "ملاحظة:", 395, 50);
            fwLogNoteB = FwBox(fwLogP, 460, 48, 235);

            var add = FwBtn(fwLogP, "➕ تسجيل عملية", 20, 86, 220, Color.FromArgb(35, 115, 75));
            add.Click += (s, e) => FlashLogAdd();
            var del = FwBtn(fwLogP, "🗑 حذف", 248, 86, 160, Color.FromArgb(150, 60, 60));
            del.Click += (s, e) => FlashLogDel();
            var stat = FwBtn(fwLogP, "📊 إحصائية", 416, 86, 160, CARD);
            stat.FlatAppearance.BorderColor = BLUE; stat.FlatAppearance.BorderSize = 1;
            stat.Click += (s, e) => FlashLogStats();

            fwLogList = new ListBox { Location = new Point(20, 128), Size = new Size(675, 438), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            fwLogP.Controls.Add(fwLogList);
        }

        void FlashLogAdd()
        {
            string model = fwLogModelB.Text.Trim();
            if (model == "") { MessageBox.Show("اكتب الموديل.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            flashLog.Insert(0, new FlashOp { Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Model = model, Tool = fwLogToolC.Text, Firmware = fwLogFwB.Text.Trim(), Result = fwLogResultC.Text, Note = fwLogNoteB.Text.Trim() });
            FlashLogSave(); FlashLogRefresh();
            fwLogModelB.Text = ""; fwLogFwB.Text = ""; fwLogNoteB.Text = "";
            LogOp("سجل تفليش: " + model + " = " + fwLogResultC.Text);
        }

        void FlashLogDel()
        {
            int i = fwLogList.SelectedIndex;
            if (i < 0 || i >= flashLog.Count) { MessageBox.Show("اختر عملية.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            flashLog.RemoveAt(i); FlashLogSave(); FlashLogRefresh();
        }

        void FlashLogStats()
        {
            int ok = flashLog.Count(x => (x.Result ?? "").Contains("نجح"));
            int fail = flashLog.Count(x => (x.Result ?? "").Contains("فشل"));
            int all = flashLog.Count;
            MessageBox.Show("إجمالي العمليات: " + all + "\r\nنجح: " + ok + "\r\nفشل: " + fail + "\r\nنسبة النجاح: " + (all > 0 ? (ok * 100 / all) : 0) + "%", "إحصائية التفليش", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void FlashLogRefresh()
        {
            if (fwLogList == null) return;
            fwLogList.Items.Clear();
            foreach (var x in flashLog)
                fwLogList.Items.Add((x.Date ?? "") + "  |  " + (x.Model ?? "") + "  |  " + (x.Tool ?? "") + "  |  " + (x.Result ?? "") + (string.IsNullOrEmpty(x.Firmware) ? "" : "  |  " + x.Firmware) + (string.IsNullOrEmpty(x.Note) ? "" : "  — " + x.Note));
        }

        void FlashLogLoad()
        {
            try { if (File.Exists(FlashLogJson)) flashLog = JsonSerializer.Deserialize<List<FlashOp>>(File.ReadAllText(FlashLogJson)) ?? new List<FlashOp>(); }
            catch { flashLog = new List<FlashOp>(); }
            FlashLogRefresh();
        }

        void FlashLogSave()
        {
            try { File.WriteAllText(FlashLogJson, JsonSerializer.Serialize(flashLog, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(true)); }
            catch (Exception ex) { ErrBox("تعذّر حفظ السجل: " + ex.Message); }
        }

        // ============ مساعدات ============
        static string FwChipFamily(string platform, string hardware)
        {
            string s = ((platform ?? "") + " " + (hardware ?? "")).ToLowerInvariant();
            if (s.Contains("msm") || s.Contains("qcom") || s.Contains("sdm") || s.Contains("snapdragon") || s.Contains("kona") || s.Contains("lahaina") || s.Contains("bengal") || s.Contains("taro") || s.Contains("lito")) return "qcom";
            if (s.Contains("mt6") || s.Contains("mt8") || s.Contains("mediatek") || s.Contains("mtk")) return "mtk";
            if (s.Contains("ums") || s.Contains("sc98") || s.Contains("sp9") || s.Contains("unisoc") || s.Contains("spreadtrum") || s.Contains("sprd") || s.Contains("sc78") || s.StartsWith("t3") || s.StartsWith("t6")) return "spd";
            if (s.Contains("rk3") || s.Contains("rockchip")) return "rk";
            if (s.Contains("sun") || s.Contains("allwinner")) return "allwinner";
            if (s.Contains("exynos") || s.Contains("universal") || s.Contains("samsungexynos")) return "exynos";
            return "unknown";
        }

        static string FwChipArabic(string k)
        {
            switch (k)
            {
                case "qcom": return "Qualcomm";
                case "mtk": return "MediaTek";
                case "spd": return "Unisoc/SPD";
                case "rk": return "Rockchip";
                case "allwinner": return "Allwinner";
                case "exynos": return "Exynos (Samsung)";
                default: return "غير معروف";
            }
        }

        static string FwToolForChip(string k)
        {
            switch (k)
            {
                case "qcom": return "QFIL / QPST (EDL 9008)";
                case "mtk": return "SP Flash Tool (scatter)";
                case "spd": return "ResearchDownload (.pac)";
                case "rk": return "RKDevTool (.img)";
                case "allwinner": return "PhoenixSuit / LiveSuit (.img)";
                case "exynos": return "Odin (Samsung .tar.md5)";
                default: return "حدّد المعالج يدوياً";
            }
        }

        static string FwFileType(string path)
        {
            string n = System.IO.Path.GetFileName(path).ToLowerInvariant();
            if (n.EndsWith(".pac")) return "Unisoc/SPD (.pac)";
            if (n.EndsWith(".tar.md5") || n.EndsWith(".tar")) return "Samsung (Odin .tar)";
            if (n.EndsWith(".ozip")) return "Oppo/Realme (.ozip)";
            if (n.EndsWith(".ofp")) return "Oppo (.ofp)";
            if (n.EndsWith(".kdz") || n.EndsWith(".tot")) return "LG (.kdz/.tot)";
            if (n.EndsWith(".ipsw")) return "Apple (.ipsw)";
            if (n.EndsWith(".img")) return "صورة قسم (.img) — Rockchip/Allwinner/عام";
            if (n.EndsWith(".zip")) return "حزمة (.zip) — OTA/fastboot/scatter";
            if (n.EndsWith(".md5")) return "بصمة/حزمة Samsung (.md5)";
            if (n.EndsWith(".bin")) return "صورة ثنائية (.bin)";
            if (n.Contains("scatter")) return "MediaTek (scatter)";
            if (n.Contains("rawprogram")) return "Qualcomm (rawprogram)";
            return "غير معروف";
        }

        static string FwChipByExt(string path)
        {
            string n = System.IO.Path.GetFileName(path).ToLowerInvariant();
            if (n.EndsWith(".pac")) return "spd";
            if (n.EndsWith(".tar.md5") || n.EndsWith(".tar")) return "exynos";
            if (n.Contains("scatter")) return "mtk";
            if (n.Contains("rawprogram")) return "qcom";
            if (n.EndsWith(".img")) return "any";
            return "";
        }

        static string FwModelToken(string model)
        {
            string m = (model ?? "").ToUpperInvariant().Trim();
            int sp = m.IndexOf(' ');
            if (sp > 0) m = m.Substring(0, sp);
            return m;
        }

        static string FwMd5(string path)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
                {
                    var hash = md5.ComputeHash(fs);
                    var sb = new StringBuilder();
                    foreach (var b in hash) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch (Exception ex) { return "تعذّر: " + ex.Message; }
        }

        static string FwSizeStr(long b)
        {
            if (b <= 0) return "0";
            string[] u = { "B", "KB", "MB", "GB" }; double v = b; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return v.ToString(i == 0 ? "0" : "0.##") + " " + u[i];
        }
    }
}
