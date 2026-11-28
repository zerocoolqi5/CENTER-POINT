// -*- coding: utf-8 -*-
// Center Point - صفحة الإعدادات (Settings) — partial class
using System;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace CenterPoint
{
    public partial class MainForm
    {
        Panel pageSettings; Button biSettings, biSmartRec;

        void BuildSettingsPage()
        {
            pageSettings = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageSettings);

            var title = new Label { Text = "⚙ الإعدادات", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageSettings.Controls.Add(title);
            Tr(title, "⚙ الإعدادات", "⚙ Settings");

            StLbl("العرض واللغة", 54);
            StBtn("🌐 تبديل اللغة (عربي / English)", 20, 84, 335, BLUE, () => ToggleLang());
            StBtn("🔄 فحص التحديثات الآن", 363, 84, 332, Color.FromArgb(35, 115, 75), () => CheckUpdate(false));

            StLbl("التحديثات", 128);
            var chkAuto = new CheckBox
            {
                Text = "التحديث التلقائي عند بدء التشغيل (لا يبدأ أثناء عملية نقل/صيانة جارية)",
                Location = new Point(20, 158), Size = new Size(675, 26), ForeColor = Color.White, BackColor = BG,
                Checked = AutoUpdateEnabled, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 9, FontStyle.Bold)
            };
            chkAuto.CheckedChanged += (s, e) => { AutoUpdateEnabled = chkAuto.Checked; };
            pageSettings.Controls.Add(chkAuto);

            StLbl("الملفات والسجلات", 196);
            StBtn("📁 فتح مجلد exports", 20, 226, 335, CARD, () => StOpen(ExportsDir));
            StBtn("📄 سجل العمليات", 363, 226, 332, CARD, () => StOpenFile(Path.Combine(ExportsDir, "operations.log")));
            StBtn("⚠ سجل الأخطاء", 20, 268, 335, CARD, () => StOpenFile(Path.Combine(ExportsDir, "errors.log")));
            StBtn("🗒 السجل المركزي (center.log)", 363, 268, 332, CARD, () => StOpenFile(Path.Combine(ExportsDir, "center.log")));

            StLbl("مزامنة البيانات بين الأجهزة", 316);
            stDataLbl = new Label
            {
                Text = "", ForeColor = Color.FromArgb(180, 230, 200), BackColor = Color.FromArgb(16, 24, 44),
                Location = new Point(20, 346), Size = new Size(675, 24), TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Arial", 9, FontStyle.Bold), Padding = new Padding(10, 0, 10, 0)
            };
            pageSettings.Controls.Add(stDataLbl);
            RefreshDataFolderLabel();
            StBtn("📂 تعيين مجلد مشترك (OneDrive/شبكة)", 20, 374, 335, Color.FromArgb(35, 115, 75), () => SetDataFolder());
            StBtn("↩ رجوع للمجلد المحلي", 363, 374, 332, CARD, () => ResetDataFolder());

            StLbl("الشبكة", 414);
            var net = new Label
            {
                Text = "البرنامج يعمل بدون إنترنت. «التنزيل» و«البحث عن الروم» و«فحص التحديثات» تحتاج اتصالاً فقط.",
                ForeColor = Color.FromArgb(200, 215, 240), BackColor = Color.FromArgb(16, 24, 44), Location = new Point(20, 442), Size = new Size(675, 34),
                TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9), Padding = new Padding(10, 0, 10, 0)
            };
            pageSettings.Controls.Add(net);

            StLbl("عن البرنامج", 486);
            var about = new Label
            {
                Text = "CENTER POINT PROFESSIONAL — V13.8.0\r\n" +
                       "سنتر بوينت للاتصالات\r\n" +
                       "واتساب / هاتف:  0782 915 5923\r\n" +
                       "⛔ أدوات تشخيص وإرشاد وإدارة — لا يتجاوز أقفال الحسابات (FRP / iCloud).",
                ForeColor = Color.FromArgb(205, 220, 245), BackColor = Color.FromArgb(10, 16, 32), Location = new Point(20, 514), Size = new Size(675, 132),
                TextAlign = ContentAlignment.TopRight, Font = new Font("Consolas", 11), Padding = new Padding(14, 12, 14, 12)
            };
            pageSettings.Controls.Add(about);
        }

        Label stDataLbl;

        void RefreshDataFolderLabel()
        {
            if (stDataLbl == null) return;
            string d = DataFolder;
            stDataLbl.Text = "المجلد الحالي: " + (string.IsNullOrWhiteSpace(d) ? "محلي (Documents\\CenterPoint Transfer\\exports)" : d);
        }

        // تعيين مجلد مشترك + نسخ البيانات الحالية إليه + إعادة تحميلها
        void SetDataFolder()
        {
            using (var dlg = new FolderBrowserDialog { Description = "اختر مجلداً مشتركاً (OneDrive / قرص شبكة) لحفظ بيانات المحل" })
            {
                try { if (!string.IsNullOrWhiteSpace(DataFolder) && Directory.Exists(DataFolder)) dlg.SelectedPath = DataFolder; } catch { }
                if (dlg.ShowDialog() != DialogResult.OK) return;
                string target = dlg.SelectedPath;
                if (string.IsNullOrWhiteSpace(target)) return;
                string[] files = { "customers.json", "invoices.json", "inventory.json", "tickets.json", "firmware_lib.json", "vendor_tools.json", "flash_log.json" };
                int copied = 0;
                try
                {
                    Directory.CreateDirectory(target);
                    string cur = DataDir; // المجلد المستخدم حالياً
                    if (!string.Equals(Path.GetFullPath(cur), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                        foreach (var fn in files)
                        {
                            string src = Path.Combine(cur, fn), dst = Path.Combine(target, fn);
                            try { if (File.Exists(src) && !File.Exists(dst)) { File.Copy(src, dst); copied++; } } catch { }
                        }
                }
                catch (Exception ex) { MessageBox.Show("تنبيه أثناء التحضير: " + ex.Message); }
                DataFolder = target;
                RefreshDataFolderLabel();
                ReloadAllData();
                MessageBox.Show("تم ضبط مجلد البيانات المشترك:\r\n" + target +
                    (copied > 0 ? "\r\n\r\nنُسخت " + copied + " ملف بيانات إليه." : "") +
                    "\r\n\r\nضع نفس المجلد (أو نفس حساب OneDrive) على بقية الأجهزة لتتزامن البيانات.",
                    "المزامنة", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        void ResetDataFolder()
        {
            if (MessageBox.Show("الرجوع للمجلد المحلي؟ البيانات في المجلد المشترك تبقى كما هي، لكن هذا الجهاز لن يعود يستخدمها.",
                "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            DataFolder = "";
            RefreshDataFolderLabel();
            ReloadAllData();
            MessageBox.Show("رجعت البيانات للمجلد المحلي الافتراضي.", "المزامنة", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // إعادة تحميل كل البيانات من المجلد الحالي (بعد تبديل المجلد)
        void ReloadAllData()
        {
            try { CustomersLoad(); } catch { }
            try { InvoicesLoad(); } catch { }
            try { InventoryLoad(); } catch { }
            try { TicketsLoad(); } catch { }
            try { FwLibLoad(); } catch { }
            try { FlashLogLoad(); } catch { }
        }

        void StLbl(string text, int y)
        {
            pageSettings.Controls.Add(new Label { Text = text, Location = new Point(20, y), Size = new Size(675, 22), ForeColor = Color.FromArgb(201, 162, 39), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 10, FontStyle.Bold) });
        }

        void StBtn(string text, int x, int y, int w, Color col, Action act)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 32), BackColor = col, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            if (col == CARD) { b.FlatAppearance.BorderColor = BLUE; b.FlatAppearance.BorderSize = 1; } else b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => { try { act(); } catch (Exception ex) { MessageBox.Show(ex.Message); } };
            pageSettings.Controls.Add(b);
        }

        void StOpen(string dir)
        {
            try { if (Directory.Exists(dir)) Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true }); else MessageBox.Show("المجلد غير موجود بعد."); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void StOpenFile(string path)
        {
            try { if (File.Exists(path)) Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); else MessageBox.Show("الملف غير موجود بعد (لا توجد سجلات حتى الآن)."); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
}
