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
            StBtn("🔄 فحص التحديثات", 363, 84, 332, Color.FromArgb(35, 115, 75), () => CheckUpdate(false));

            StLbl("الملفات والسجلات", 134);
            StBtn("📁 فتح مجلد exports", 20, 164, 335, CARD, () => StOpen(ExportsDir));
            StBtn("📄 سجل العمليات", 363, 164, 332, CARD, () => StOpenFile(Path.Combine(ExportsDir, "operations.log")));
            StBtn("⚠ سجل الأخطاء", 20, 206, 335, CARD, () => StOpenFile(Path.Combine(ExportsDir, "errors.log")));
            StBtn("🗒 السجل المركزي (center.log)", 363, 206, 332, CARD, () => StOpenFile(Path.Combine(ExportsDir, "center.log")));

            StLbl("الشبكة", 256);
            var net = new Label
            {
                Text = "البرنامج يعمل بدون إنترنت. أزرار «التنزيل» و«البحث عن الروم» تحتاج اتصالاً لفتح المتصفح فقط. لا تُرسل أي بيانات خارج الجهاز.",
                ForeColor = Color.FromArgb(200, 215, 240), BackColor = Color.FromArgb(16, 24, 44), Location = new Point(20, 286), Size = new Size(675, 46),
                TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9), Padding = new Padding(10, 0, 10, 0)
            };
            pageSettings.Controls.Add(net);

            StLbl("عن البرنامج", 348);
            var about = new Label
            {
                Text = "CENTER POINT PROFESSIONAL — V12.0.0\r\n" +
                       "سنتر بوينت للاتصالات\r\n" +
                       "نقل البيانات + صيانة الأجهزة + التفليش + إدارة المحل\r\n\r\n" +
                       "واتساب / هاتف:  0782 915 5923\r\n" +
                       "فيسبوك:  facebook.com/centerpoint\r\n\r\n" +
                       "⛔ البرنامج أدوات تشخيص وإرشاد وإدارة — لا يتجاوز أقفال الحسابات (FRP / iCloud).",
                ForeColor = Color.FromArgb(205, 220, 245), BackColor = Color.FromArgb(10, 16, 32), Location = new Point(20, 378), Size = new Size(675, 240),
                TextAlign = ContentAlignment.TopRight, Font = new Font("Consolas", 11), Padding = new Padding(14, 12, 14, 12)
            };
            pageSettings.Controls.Add(about);
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
