// -*- coding: utf-8 -*-
// Center Point - مركز التفليش الموحّد (كل الماركات + خانات Odin + دليل + بحث روم) — partial class
// آمن: يرشد ويرتّب ويتحقق ويفتح أداة الشركة — لا يُنفّذ بروتوكول تفليش داخلياً، ولا يتجاوز أي قفل حساب (FRP).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace CenterPoint
{
    public partial class MainForm
    {
        Panel pageSamsung; Button biSamsung;
        ComboBox smBrand; TextBox smModel;
        string[] smPaths = new string[6];          // 0=PIT 1=BL 2=AP 3=CP 4=CSC 5=USRDATA
        Label[] smVals = new Label[6];
        readonly string[] smCaps = { "PIT", "BL", "AP", "CP", "CSC", "USRDATA" };
        readonly string[] smHint = { "ملف .pit (التقسيم)", "BL_ البوتلودر", "AP_ النظام (الأكبر)", "CP_ المودم", "CSC_ المنطقة / HOME_CSC_ يحفظ البيانات", "USERDATA (اختياري)" };
        readonly string[] smBrandKeys = { "samsung", "xiaomi", "oppo", "vivo", "huawei", "moto", "tecno", "lg", "iphone", "qcom", "mtk", "spd", "rk", "allwinner" };
        CheckBox smAutoReboot, smHomeCsc, smRepartition;
        TextBox smOdinPath, smResult;

        void BuildSamsungPage()
        {
            pageSamsung = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageSamsung);

            var title = new Label { Text = "⚡ مركز التفليش الموحّد", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 10), Size = new Size(675, 26), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageSamsung.Controls.Add(title);
            Tr(title, "⚡ مركز التفليش الموحّد", "⚡ Unified Flashing Center");

            var note = new Label
            {
                Text = "اختر الماركة ← اعرض الدليل، نظّم الملفات بخاناتها (Samsung/Odin) وتحقّق منها، ابحث عن الروم، ثم افتح أداة الشركة. لا تفليش داخلي، ولا تجاوز FRP.",
                ForeColor = Color.White, BackColor = Color.FromArgb(30, 60, 110), Location = new Point(20, 42), Size = new Size(675, 34),
                TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9), Padding = new Padding(8, 0, 8, 0)
            };
            pageSamsung.Controls.Add(note);

            // صف الماركة + الدليل
            smBrand = new ComboBox { Location = new Point(175, 82), Size = new Size(380, 26), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = CARD, ForeColor = Color.White, Font = new Font("Arial", 9) };
            smBrand.Items.AddRange(new object[] {
                "Samsung — Odin / Thor", "Xiaomi / Redmi / POCO — Mi Flash", "Oppo / Realme / OnePlus", "Vivo / iQOO",
                "Huawei / Honor — eRecovery", "Motorola / Lenovo / Nokia / Pixel — Fastboot", "Tecno / Infinix / itel — SP Flash Tool",
                "LG — LGUP", "iPhone / iPad — iTunes/DFU", "Qualcomm — QFIL", "MediaTek — SP Flash Tool",
                "Unisoc / SPD — ResearchDownload", "Rockchip — RKDevTool", "Allwinner — PhoenixSuit",
            });
            smBrand.SelectedIndex = 0;
            pageSamsung.Controls.Add(smBrand);
            var brLbl = new Label { Text = "الماركة:", Location = new Point(560, 84), Size = new Size(135, 22), ForeColor = Color.FromArgb(201, 162, 39), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold) };
            pageSamsung.Controls.Add(brLbl);
            var guideBtn = new Button { Text = "📖 الدليل", Location = new Point(20, 82), Size = new Size(150, 28), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            guideBtn.FlatAppearance.BorderColor = BLUE; guideBtn.Click += (s, e) => SmGuide();
            pageSamsung.Controls.Add(guideBtn);

            // صف البحث عن الروم
            var searchBtn = new Button { Text = "🔎 بحث عن الروم", Location = new Point(20, 116), Size = new Size(185, 28), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            searchBtn.FlatAppearance.BorderSize = 0; searchBtn.Click += (s, e) => SmSearch();
            pageSamsung.Controls.Add(searchBtn);
            smModel = new TextBox { Location = new Point(215, 117), Size = new Size(480, 26), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Consolas", 10) };
            pageSamsung.Controls.Add(smModel);

            // مسار الأداة (Odin) + تنزيل
            var odLbl = new Label { Text = "أداة:", Location = new Point(565, 150), Size = new Size(130, 22), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold) };
            pageSamsung.Controls.Add(odLbl);
            smOdinPath = new TextBox { Location = new Point(190, 148), Size = new Size(370, 26), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), ReadOnly = true };
            pageSamsung.Controls.Add(smOdinPath);
            var odPick = new Button { Text = "📂", Location = new Point(20, 147), Size = new Size(44, 28), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            odPick.FlatAppearance.BorderColor = BLUE; odPick.Click += (s, e) => SmPickOdin();
            pageSamsung.Controls.Add(odPick);
            var odGet = new Button { Text = "⬇ تنزيل Odin", Location = new Point(66, 147), Size = new Size(118, 28), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            odGet.FlatAppearance.BorderColor = BLUE; odGet.Click += (s, e) => SmOpenUrl("https://odindownload.com/");
            pageSamsung.Controls.Add(odGet);

            // صف التحكم
            var stepBtn = new Button { Text = "📋 خطوات", Location = new Point(20, 182), Size = new Size(160, 28), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            stepBtn.FlatAppearance.BorderColor = BLUE; stepBtn.Click += (s, e) => SmSteps();
            var verBtn = new Button { Text = "✔ تحقّق", Location = new Point(188, 182), Size = new Size(160, 28), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            verBtn.FlatAppearance.BorderSize = 0; verBtn.Click += (s, e) => SmVerify();
            var autoBtn = new Button { Text = "🔎 اكتشاف من مجلد", Location = new Point(356, 182), Size = new Size(170, 28), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            autoBtn.FlatAppearance.BorderSize = 0; autoBtn.Click += (s, e) => SmAutoDetect();
            var odBtn = new Button { Text = "🚀 فتح الأداة", Location = new Point(534, 182), Size = new Size(161, 28), BackColor = Color.FromArgb(150, 90, 20), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            odBtn.FlatAppearance.BorderSize = 0; odBtn.Click += (s, e) => SmOpenOdin();
            pageSamsung.Controls.Add(stepBtn); pageSamsung.Controls.Add(verBtn); pageSamsung.Controls.Add(autoBtn); pageSamsung.Controls.Add(odBtn);

            // خانات الملفات الستة (Samsung/Odin)
            int y = 216;
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                var cap = new Label { Text = smCaps[i], Location = new Point(620, y + 3), Size = new Size(75, 22), ForeColor = Color.FromArgb(120, 200, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Consolas", 11, FontStyle.Bold) };
                pageSamsung.Controls.Add(cap);
                var pick = new Button { Text = "📂 اختيار", Location = new Point(520, y), Size = new Size(95, 28), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
                pick.FlatAppearance.BorderColor = BLUE; pick.Click += (s, e) => SmPickSlot(idx);
                pageSamsung.Controls.Add(pick);
                var clr = new Button { Text = "✖", Location = new Point(470, y), Size = new Size(44, 28), BackColor = Color.FromArgb(120, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
                clr.FlatAppearance.BorderSize = 0; clr.Click += (s, e) => { smPaths[idx] = null; SmUpdateSlot(idx); };
                pageSamsung.Controls.Add(clr);
                smVals[i] = new Label { Text = "— " + smHint[i], Location = new Point(20, y + 3), Size = new Size(444, 22), ForeColor = Color.FromArgb(160, 180, 210), BackColor = Color.FromArgb(14, 20, 38), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Consolas", 9), Padding = new Padding(0, 0, 6, 0) };
                pageSamsung.Controls.Add(smVals[i]);
                y += 31;
            }

            // الخيارات
            smAutoReboot = SmChk(pageSamsung, "Auto Reboot", 560, y + 2, true);
            smHomeCsc = SmChk(pageSamsung, "HOME_CSC (حفظ البيانات)", 320, y + 2, true);
            smRepartition = SmChk(pageSamsung, "Re-Partition (خطر)", 20, y + 2, false);
            smRepartition.ForeColor = Color.FromArgb(240, 150, 90);
            y += 32;

            smResult = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(255, 200, 120), Location = new Point(20, y), Size = new Size(675, 644 - y), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            smResult.Text = "خطوات سريعة:\r\n1) اختر الماركة ← «📖 الدليل» (أداة الشركة + الخطوات).\r\n2) Samsung: «اكتشاف من مجلد» يرتّب BL/AP/CP/CSC بخاناتها ← «تحقّق».\r\n3) «بحث عن الروم» يفتح مصادر الفيرموير بالموديل.\r\n4) «فتح الأداة» ← نفّذ التفليش بأداة الشركة.\r\n\r\n⛔ الفيرموير يطابق الموديل بالضبط. CSC يمسح، HOME_CSC يحفظ البيانات.";
            pageSamsung.Controls.Add(smResult);

            for (int i = 0; i < 6; i++) SmUpdateSlot(i);
        }

        string SmKey() { int i = smBrand != null ? smBrand.SelectedIndex : 0; return (i >= 0 && i < smBrandKeys.Length) ? smBrandKeys[i] : "samsung"; }

        void SmGuide() { try { SetTb(smResult, FlGuideText(SmKey())); } catch { } }

        void SmSearch()
        {
            string m = (smModel.Text ?? "").Trim();
            if (m == "") { MessageBox.Show("اكتب الموديل أولاً (مثال: SM-A346E).", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string q = Uri.EscapeDataString(m + " firmware flash file");
            SmOpenUrl("https://www.google.com/search?q=" + q);
            SmOpenUrl("https://www.needrom.com/?s=" + Uri.EscapeDataString(m));
            SetTb(smResult, "فُتحت مصادر البحث عن روم: " + m + "\r\n• Google (نتائج عامة)\r\n• NeedRom\r\n\r\nحمّل الفيرموير المطابق للموديل بالضبط فقط.");
        }

        CheckBox SmChk(Panel p, string text, int x, int y, bool on)
        {
            var c = new CheckBox { Text = text, Location = new Point(x, y), Size = new Size(230, 24), ForeColor = Color.White, BackColor = BG, Checked = on, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 9, FontStyle.Bold) };
            p.Controls.Add(c); return c;
        }

        void SmPickOdin()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "اختر ملف Odin التنفيذي";
                dlg.Filter = "Odin|Odin3*.exe;Odin*.exe|برامج|*.exe|كل الملفات|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                smOdinPath.Text = dlg.FileName;
            }
        }

        void SmPickSlot(int idx)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "اختر ملف " + smCaps[idx];
                dlg.Filter = (idx == 0) ? "PIT|*.pit|كل الملفات|*.*" : "فيرموير|*.tar.md5;*.tar;*.md5;*.lz4|كل الملفات|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                smPaths[idx] = dlg.FileName;
            }
            SmUpdateSlot(idx);
        }

        void SmUpdateSlot(int idx)
        {
            if (smVals[idx] == null) return;
            if (string.IsNullOrEmpty(smPaths[idx]) || !File.Exists(smPaths[idx]))
            {
                smVals[idx].Text = "— " + smHint[idx];
                smVals[idx].ForeColor = Color.FromArgb(160, 180, 210);
                return;
            }
            string nm = Path.GetFileName(smPaths[idx]);
            long sz = 0; try { sz = new FileInfo(smPaths[idx]).Length; } catch { }
            smVals[idx].Text = nm + "   (" + SmSize(sz) + ")";
            smVals[idx].ForeColor = SmSlotOk(idx, nm) ? Color.FromArgb(120, 230, 150) : Color.FromArgb(240, 170, 90);
        }

        static int SmClassify(string fname)
        {
            string n = (fname ?? "").ToUpperInvariant();
            if (n.EndsWith(".PIT")) return 0;
            if (n.StartsWith("BL_")) return 1;
            if (n.StartsWith("AP_")) return 2;
            if (n.StartsWith("CP_")) return 3;
            if (n.StartsWith("HOME_CSC_") || n.StartsWith("CSC_")) return 4;
            if (n.StartsWith("USERDATA") || n.StartsWith("USRDATA")) return 5;
            return -1;
        }

        bool SmSlotOk(int idx, string fname)
        {
            int c = SmClassify(fname);
            if (c == -1) return idx != 0;
            return c == idx;
        }

        void SmAutoDetect()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "اختر مجلد الفيرموير المفكوك (فيه ملفات BL/AP/CP/CSC...)";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string dir = dlg.SelectedPath;
                string[] files;
                try { files = Directory.GetFiles(dir); } catch { files = new string[0]; }
                int found = 0;
                var sb = new StringBuilder();
                sb.AppendLine("═══ نتيجة الاكتشاف ═══");
                sb.AppendLine("المجلد: " + dir);
                sb.AppendLine("");
                foreach (var f in files)
                {
                    int c = SmClassify(Path.GetFileName(f));
                    if (c >= 0 && string.IsNullOrEmpty(smPaths[c]))
                    {
                        smPaths[c] = f; SmUpdateSlot(c); found++;
                        sb.AppendLine("✔ " + smCaps[c] + "  ←  " + Path.GetFileName(f));
                    }
                }
                if (found == 0) sb.AppendLine("لم يُعثر على ملفات بأسماء قياسية (BL_/AP_/CP_/CSC_/.pit). اختر الملفات يدوياً.");
                else sb.AppendLine("\r\nرُتّبت " + found + " ملف. اضغط «تحقّق» ثم «فتح الأداة».");
                SetTb(smResult, sb.ToString());
            }
        }

        void SmVerify()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══ تحقّق الملفات ═══");
            sb.AppendLine("");
            bool anyAp = false; int ok = 0;
            for (int i = 0; i < 6; i++)
            {
                if (string.IsNullOrEmpty(smPaths[i])) { sb.AppendLine("○ " + smCaps[i] + " : (فارغ)"); continue; }
                if (!File.Exists(smPaths[i])) { sb.AppendLine("✖ " + smCaps[i] + " : الملف غير موجود"); continue; }
                string nm = Path.GetFileName(smPaths[i]);
                long sz = 0; try { sz = new FileInfo(smPaths[i]).Length; } catch { }
                bool good = SmSlotOk(i, nm);
                if (i == 2) anyAp = true;
                if (good) ok++;
                sb.AppendLine((good ? "✔ " : "⚠ ") + smCaps[i] + " : " + nm + "  (" + SmSize(sz) + ")" + (good ? "" : "  ← اسم غير متوقّع لهذه الخانة"));
            }
            sb.AppendLine("");
            if (!anyAp) sb.AppendLine("⛔ تحذير: خانة AP فارغة — غالباً مطلوبة للتفليش الكامل.");
            sb.AppendLine(ok > 0 ? "جاهز. افتح الأداة وضع كل ملف بخانته كما هو مرتّب." : "لا توجد ملفات صالحة بعد.");
            sb.AppendLine("تذكير: الفيرموير يطابق الموديل بالضبط. CSC يمسح، HOME_CSC يحفظ البيانات.");
            SetTb(smResult, sb.ToString());
        }

        void SmOpenOdin()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══ ضع الملفات في الأداة هكذا ═══");
            sb.AppendLine("");
            for (int i = 0; i < 6; i++)
                sb.AppendLine(smCaps[i].PadRight(8) + " : " + (string.IsNullOrEmpty(smPaths[i]) ? "(فارغ)" : Path.GetFileName(smPaths[i])));
            sb.AppendLine("");
            sb.AppendLine("الخيارات: Auto Reboot=" + (smAutoReboot.Checked ? "✔" : "✖") + "  |  Re-Partition=" + (smRepartition.Checked ? "✔ (خطر)" : "✖") + "  |  " + (smHomeCsc.Checked ? "استخدم HOME_CSC" : "CSC كامل (يمسح)"));
            sb.AppendLine("");
            sb.AppendLine("Samsung — Download Mode: أطفئه ← امسك (خفض الصوت + رفع الصوت/Bixby) ووصّل USB ← رفع الصوت للتأكيد.");
            SetTb(smResult, sb.ToString());

            string odin = smOdinPath.Text.Trim();
            if (odin == "" || !File.Exists(odin))
            {
                if (MessageBox.Show("لم يُحدَّد مسار الأداة (Odin.exe).\r\nتحب تختاره الآن؟ (أو نزّله من زر «تنزيل Odin»)\r\n\r\nلغير Samsung: استخدم «📖 الدليل» لأداة الشركة.", "الأداة", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    SmPickOdin(); odin = smOdinPath.Text.Trim();
                }
            }
            if (odin != "" && File.Exists(odin))
            {
                try { Process.Start(new ProcessStartInfo { FileName = odin, UseShellExecute = true }); LogOp("فتح أداة التفليش"); }
                catch (Exception ex) { ErrBox("تعذّر فتح الأداة: " + ex.Message); }
            }
        }

        void SmSteps() { SmGuide(); }

        void SmOpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
        }

        static string SmSize(long b)
        {
            if (b <= 0) return "0";
            string[] u = { "B", "KB", "MB", "GB" }; double v = b; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return v.ToString(i == 0 ? "0" : "0.##") + " " + u[i];
        }
    }
}
