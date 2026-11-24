// -*- coding: utf-8 -*-
// Center Point - لوحة التحكم (Dashboard) — partial class
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
        Panel pageDash; Button biDash;
        Label dsOps, dsErr, dsRep, dsDev, dsTick; TextBox dashOps;

        void BuildDashPage()
        {
            pageDash = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageDash);

            var title = new Label { Text = "📊 لوحة التحكم", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(500, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageDash.Controls.Add(title);
            Tr(title, "📊 لوحة التحكم", "📊 Dashboard");
            var refBtn = new Button { Text = "🔄 تحديث", Location = new Point(535, 12), Size = new Size(160, 30), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            refBtn.FlatAppearance.BorderSize = 0; refBtn.Click += (s, e) => DashRefresh();
            pageDash.Controls.Add(refBtn);

            // بطاقات الإحصائيات
            dsDev = MakeStatTile(pageDash, 20, "📱", "الأجهزة المتصلة", Color.FromArgb(47, 139, 255));
            dsOps = MakeStatTile(pageDash, 157, "📈", "إجمالي العمليات", Color.FromArgb(34, 195, 94));
            dsRep = MakeStatTile(pageDash, 294, "📄", "التقارير", Color.FromArgb(129, 64, 224));
            dsErr = MakeStatTile(pageDash, 431, "⚠", "الأخطاء", Color.FromArgb(230, 80, 80));
            dsTick = MakeStatTile(pageDash, 568, "🎫", "تذاكر مفتوحة", Color.FromArgb(240, 145, 46));

            // أزرار سريعة
            var qLbl = new Label { Text = "إجراءات سريعة:", Location = new Point(20, 150), Size = new Size(675, 22), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 10, FontStyle.Bold) };
            pageDash.Controls.Add(qLbl);
            AddQuick(pageDash, 20, "📲 نقل البيانات", Color.FromArgb(129, 64, 224), "transfer");
            AddQuick(pageDash, 194, "🔧 مركز الإصلاح", Color.FromArgb(47, 139, 255), "repair");
            AddQuick(pageDash, 368, "💾 نسخ احتياطي", Color.FromArgb(34, 195, 94), "backup");
            AddQuick(pageDash, 542, "🎫 تذاكر الصيانة", Color.FromArgb(240, 145, 46), "tickets");

            // آخر العمليات
            var opLbl = new Label { Text = "آخر العمليات:", Location = new Point(20, 228), Size = new Size(675, 22), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 10, FontStyle.Bold) };
            pageDash.Controls.Add(opLbl);
            dashOps = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(120, 190, 255), Location = new Point(20, 254), Size = new Size(675, 384), Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
            pageDash.Controls.Add(dashOps);
        }

        Label MakeStatTile(Panel parent, int x, string icon, string cap, Color col)
        {
            Color cardBg = Color.FromArgb(22, 32, 60);
            var card = new Panel { Location = new Point(x, 50), Size = new Size(127, 96), BackColor = cardBg };
            try { using (var gp = RRect(new Rectangle(0, 0, card.Width, card.Height), 14)) card.Region = new Region(gp); } catch { }
            // شريط لون علوي
            var accent = new Panel { Location = new Point(14, 8), Size = new Size(99, 4), BackColor = col };
            var ic = new Label { Text = icon, ForeColor = col, BackColor = cardBg, Location = new Point(0, 14), Size = new Size(127, 22), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Emoji", 12) };
            var val = new Label { Text = "0", ForeColor = Color.White, BackColor = cardBg, Location = new Point(0, 36), Size = new Size(127, 34), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 20, FontStyle.Bold) };
            var lab = new Label { Text = cap, ForeColor = Color.FromArgb(170, 190, 220), BackColor = cardBg, Location = new Point(0, 72), Size = new Size(127, 20), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 8, FontStyle.Bold) };
            card.Controls.Add(accent); card.Controls.Add(ic); card.Controls.Add(val); card.Controls.Add(lab);
            parent.Controls.Add(card);
            return val;
        }

        static System.Drawing.Drawing2D.GraphicsPath RRect(Rectangle r, int rad)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void AddQuick(Panel parent, int x, string text, Color col, string page)
        {
            var b = new Button { Text = text, Location = new Point(x, 176), Size = new Size(153, 42), BackColor = col, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0; b.Click += (s, e) => ShowPage(page);
            parent.Controls.Add(b);
        }

        void DashRefresh()
        {
            try
            {
                string exp = ExportsDir;
                if (dsDev != null) dsDev.Text = devices.Count(x => x.Kind == "android" || x.Kind == "ios" || x.Kind == "mtp").ToString();
                if (dsOps != null) dsOps.Text = CountLines(Path.Combine(exp, "operations.log")).ToString();
                if (dsErr != null) dsErr.Text = CountLines(Path.Combine(exp, "errors.log")).ToString();
                int reps = 0; try { reps = Directory.GetFiles(exp, "*.txt").Length; } catch { }
                if (dsRep != null) dsRep.Text = reps.ToString();
                if (dsTick != null) dsTick.Text = OpenTicketsCount().ToString();
                // تنبيه نقص المخزون
                int low = 0; try { low = LowStockCount(); } catch { }
                string alert = low > 0 ? "⚠ تنبيه: " + low + " قطعة تحت الحد الأدنى بالمخزون\r\n\r\n" : "";
                // آخر العمليات
                var lines = ReadTail(Path.Combine(exp, "operations.log"), 14);
                dashOps.Text = alert + (lines.Count == 0 ? "لا توجد عمليات مسجّلة بعد." : string.Join("\r\n", lines));
            }
            catch { }
        }

        static int CountLines(string path)
        {
            try { return File.Exists(path) ? File.ReadAllLines(path).Count(x => x.Trim() != "") : 0; } catch { return 0; }
        }

        static List<string> ReadTail(string path, int n)
        {
            try
            {
                if (!File.Exists(path)) return new List<string>();
                var all = File.ReadAllLines(path).Where(x => x.Trim() != "").ToList();
                return all.Skip(Math.Max(0, all.Count - n)).Reverse().ToList();
            }
            catch { return new List<string>(); }
        }
    }
}
