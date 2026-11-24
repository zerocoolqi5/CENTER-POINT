// -*- coding: utf-8 -*-
// Center Point - قسم ADB Manager (partial class)
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

namespace CenterPoint
{
    public partial class MainForm
    {
        // ============ ADB Manager — أوامر ADB جاهزة ============
        void BuildAdbPage()
        {
            pageAdb = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageAdb);

            var title = new Label { Text = "🤖 ADB Manager", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageAdb.Controls.Add(title);
            Tr(title, "🤖 ADB Manager", "🤖 ADB Manager");

            var note = new Label { Text = "أوامر ADB الجاهزة: معلومات الأجهزة، إعادة التشغيل، تثبيت/إزالة APK، وتنفيذ أوامر shell. العمليات الخطرة بتأكيد.", ForeColor = Color.White, BackColor = Color.FromArgb(30, 60, 110), Location = new Point(20, 46), Size = new Size(675, 32), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9), Padding = new Padding(8, 0, 8, 0) };
            pageAdb.Controls.Add(note);

            // صف 1: معلومات + إعادة تشغيل
            var infoBtn = new Button { Text = "🔄 الأجهزة", Location = new Point(20, 84), Size = new Size(130, 30), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            infoBtn.FlatAppearance.BorderSize = 0; infoBtn.Click += (s, e) => AdbInfo();
            var rbBtn = new Button { Text = "🔄 إعادة عادية", Location = new Point(158, 84), Size = new Size(130, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            rbBtn.FlatAppearance.BorderColor = BLUE; rbBtn.Click += (s, e) => AdbReboot("", "إعادة تشغيل عادية");
            var rcBtn = new Button { Text = "🔧 Recovery", Location = new Point(296, 84), Size = new Size(130, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            rcBtn.FlatAppearance.BorderColor = BLUE; rcBtn.Click += (s, e) => AdbReboot("recovery", "الدخول إلى Recovery");
            var blBtn = new Button { Text = "⚡ Bootloader", Location = new Point(434, 84), Size = new Size(130, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 8, FontStyle.Bold) };
            blBtn.FlatAppearance.BorderColor = BLUE; blBtn.Click += (s, e) => AdbReboot("bootloader", "الدخول إلى Bootloader");
            pageAdb.Controls.Add(infoBtn); pageAdb.Controls.Add(rbBtn); pageAdb.Controls.Add(rcBtn); pageAdb.Controls.Add(blBtn);

            // صف 2: تثبيت/إزالة APK
            var instBtn = new Button { Text = "📲 تثبيت APK", Location = new Point(20, 120), Size = new Size(150, 30), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            instBtn.FlatAppearance.BorderSize = 0; instBtn.Click += (s, e) => AdbInstallApk();
            var uninBtn = new Button { Text = "🗑 إزالة تطبيق", Location = new Point(178, 120), Size = new Size(140, 30), BackColor = Color.FromArgb(150, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            uninBtn.FlatAppearance.BorderSize = 0; uninBtn.Click += (s, e) => AdbUninstall();
            adbPkg = new TextBox { Location = new Point(326, 121), Size = new Size(369, 28), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Consolas", 10) };
            pageAdb.Controls.Add(instBtn); pageAdb.Controls.Add(uninBtn); pageAdb.Controls.Add(adbPkg);
            var pkgLbl = new Label { Text = "اسم الحزمة للإزالة ↑", Location = new Point(326, 150), Size = new Size(369, 16), ForeColor = Color.FromArgb(159, 179, 214), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 7) };
            pageAdb.Controls.Add(pkgLbl);

            // صف 3: shell سريع
            var runBtn = new Button { Text = "▶ تنفيذ shell", Location = new Point(20, 170), Size = new Size(150, 30), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            runBtn.FlatAppearance.BorderSize = 0; runBtn.Click += (s, e) => AdbRunShell();
            adbCmd = new TextBox { Location = new Point(178, 171), Size = new Size(517, 28), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.FromArgb(120, 255, 170), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 10) };
            adbCmd.Text = "getprop ro.product.model";
            pageAdb.Controls.Add(runBtn); pageAdb.Controls.Add(adbCmd);

            adbResult = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.No, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(120, 190, 255), Location = new Point(20, 208), Size = new Size(675, 420), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            adbResult.Text = "أوامر ADB الجاهزة. اضغط «الأجهزة» لعرض المتصل، أو اكتب أمر shell ونفّذه.\r\nالإزالة وبعض الأوامر قد تؤثر على الجهاز — تُنفّذ بتأكيد.";
            pageAdb.Controls.Add(adbResult);
        }

        // لا ننفّذ أوامر تغيّر حالة الجهاز على أول جهاز عشوائياً.
        // عند تعدد الأجهزة يجب أن يحدد المستخدم جهازاً واحداً قبل المتابعة.
        Device GetSingleAndroidTarget()
        {
            var connected = devices.Where(x => x.Kind == "android").ToList();
            if (connected.Count == 0)
            {
                MessageBox.Show("لا يوجد جهاز أندرويد متصل عبر ADB.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (connected.Count > 1)
            {
                MessageBox.Show("تم اكتشاف أكثر من جهاز Android. افصل الأجهزة غير المطلوبة واترك الجهاز المستهدف فقط، ثم أعد المحاولة.", "تحديد الجهاز المستهدف", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return connected[0];
        }

        void AdbInfo()
        {
            SetTb(adbResult, "جاري قراءة الأجهزة...");
            Task.Run(() =>
            {
                var sb = new StringBuilder();
                var r = Shell.Run("adb", "devices -l", 20000);
                sb.AppendLine("═══ adb devices -l ═══");
                sb.AppendLine(Trim(r.outp));
                sb.AppendLine("");
                var d = devices.FirstOrDefault(x => x.Kind == "android");
                if (d != null)
                {
                    sb.AppendLine("● الجهاز الأساسي: " + Devices.Prop(d, "ro.product.manufacturer") + " " + Devices.Prop(d, "ro.product.model"));
                    sb.AppendLine("  المعرّف: " + d.Id);
                    sb.AppendLine("  أندرويد: " + Devices.Prop(d, "ro.build.version.release"));
                }
                SetTb(adbResult, sb.ToString());
            });
        }

        void AdbReboot(string arg, string label)
        {
            var d = GetSingleAndroidTarget();
            if (d == null) return;
            if (MessageBox.Show(label + "؟ (آمنة — إعادة تشغيل فقط، لا تمسح بيانات)", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            SetTb(adbResult, "جاري: adb reboot " + arg + " ...");
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + Shell.Q(d.Id) + " reboot " + arg, 25000);
                SetTb(adbResult, "✔ تم: adb reboot " + arg + "\r\n" + Trim(r.outp));
                LogOp("ADB reboot " + arg);
            });
        }

        void AdbInstallApk()
        {
            var d = GetSingleAndroidTarget();
            if (d == null) return;
            string apk = "";
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "اختر ملف APK"; dlg.Filter = "تطبيقات أندرويد|*.apk;*.apks;*.xapk|كل الملفات|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                apk = dlg.FileName;
            }
            SetTb(adbResult, "جاري تثبيت: " + Path.GetFileName(apk) + " ...");
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + Shell.Q(d.Id) + " install -r " + Shell.Q(apk), 600000);
                SetTb(adbResult, "═══ تثبيت APK ═══\r\n" + Trim(r.outp));
                LogOp("ADB install: " + Path.GetFileName(apk));
            });
        }

        void AdbUninstall()
        {
            var d = GetSingleAndroidTarget();
            if (d == null) return;
            string pkg = (adbPkg == null ? "" : adbPkg.Text).Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(pkg, @"^[A-Za-z0-9_]+(\.[A-Za-z0-9_]+)+$")) { MessageBox.Show("اسم الحزمة غير صالح. استخدم صيغة مثل com.example.app.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (MessageBox.Show("إزالة التطبيق: " + pkg + "\r\n\r\nهذا يحذف التطبيق من الجهاز. متابعة؟", "تأكيد الإزالة", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            SetTb(adbResult, "جاري إزالة: " + pkg + " ...");
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + Shell.Q(d.Id) + " uninstall " + Shell.Q(pkg), 60000);
                SetTb(adbResult, "═══ إزالة تطبيق ═══\r\n" + pkg + "\r\n" + Trim(r.outp));
                LogOp("ADB uninstall: " + pkg);
            });
        }

        void AdbRunShell()
        {
            var d = GetSingleAndroidTarget();
            if (d == null) return;
            string cmd = (adbCmd == null ? "" : adbCmd.Text).Trim();
            if (cmd == "") { MessageBox.Show("اكتب أمر shell.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            SetTb(adbResult, "$ adb shell " + cmd + "\r\n...");
            Task.Run(() =>
            {
                var r = Shell.Run("adb", "-s " + Shell.Q(d.Id) + " shell " + cmd, 120000);
                SetTb(adbResult, "$ adb shell " + cmd + "\r\n\r\n" + Trim(r.outp));
                LogOp("ADB shell: " + cmd);
            });
        }
    }
}
