// -*- coding: utf-8 -*-
// Center Point - التقارير المالية + البحث السريع الموحّد (Business Reports) — partial class
// يعتمد فقط على بيانات موجودة: invoiceList / partList / ticketList / customerList
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CenterPoint
{
    public partial class MainForm
    {
        Panel pageBiz; Button biBiz;
        Label bzToday, bzMonth, bzYear, bzTotal, bzSummary;
        TextBox bzWeek, bzMonths; TextBox bzSearchBox; ListBox bzResults;

        void BuildBizPage()
        {
            pageBiz = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageBiz);

            var title = new Label { Text = "📈 التقارير المالية", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 10), Size = new Size(420, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageBiz.Controls.Add(title); Tr(title, "📈 التقارير المالية", "📈 Financial Reports");

            var rf = new Button { Text = "🔄 تحديث", Location = new Point(470, 12), Size = new Size(105, 30), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            rf.FlatAppearance.BorderSize = 0; rf.Click += (s, e) => BizLoad();
            pageBiz.Controls.Add(rf); Tr(rf, "🔄 تحديث", "🔄 Refresh");
            var ex = new Button { Text = "🖨 تصدير تقرير", Location = new Point(583, 12), Size = new Size(112, 30), BackColor = Color.FromArgb(34, 160, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            ex.FlatAppearance.BorderSize = 0; ex.Click += (s, e) => BizExport();
            pageBiz.Controls.Add(ex); Tr(ex, "🖨 تصدير تقرير", "🖨 Export Report");
            var ts = new Button { Text = "📊 إحصائية النقل", Location = new Point(300, 12), Size = new Size(160, 30), BackColor = Color.FromArgb(129, 64, 224), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            ts.FlatAppearance.BorderSize = 0; ts.Click += (s, e) => TransferStats();
            pageBiz.Controls.Add(ts); Tr(ts, "📊 إحصائية النقل", "📊 Transfer Stats");

            // بطاقات الإيرادات
            bzToday = BizTile(pageBiz, 20, "📅", "إيراد اليوم", Color.FromArgb(34, 195, 94));
            bzMonth = BizTile(pageBiz, 194, "🗓", "إيراد الشهر", Color.FromArgb(47, 139, 255));
            bzYear = BizTile(pageBiz, 368, "📆", "إيراد السنة", Color.FromArgb(129, 64, 224));
            bzTotal = BizTile(pageBiz, 542, "💰", "الإجمالي الكلي", Color.FromArgb(240, 145, 46));

            // ملخّص
            var card = new Panel { Location = new Point(20, 150), Size = new Size(675, 70), BackColor = CARD };
            try { using (var gp = RRect(new Rectangle(0, 0, card.Width, card.Height), 12)) card.Region = new Region(gp); } catch { }
            pageBiz.Controls.Add(card);
            bzSummary = new Label { Text = "—", ForeColor = Color.White, BackColor = CARD, Location = new Point(12, 8), Size = new Size(651, 54), TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 10, FontStyle.Bold) };
            card.Controls.Add(bzSummary);

            // تفصيل آخر 7 أيام + آخر 6 أشهر
            var l7 = new Label { Text = "آخر 7 أيام:", Location = new Point(365, 226), Size = new Size(330, 20), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 10, FontStyle.Bold) };
            pageBiz.Controls.Add(l7); Tr(l7, "آخر 7 أيام:", "Last 7 days:");
            bzWeek = new TextBox { Multiline = true, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(120, 190, 255), Location = new Point(365, 250), Size = new Size(330, 150), Font = new Font("Consolas", 9), BorderStyle = BorderStyle.FixedSingle };
            pageBiz.Controls.Add(bzWeek);

            var l6 = new Label { Text = "آخر 6 أشهر:", Location = new Point(20, 226), Size = new Size(330, 20), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 10, FontStyle.Bold) };
            pageBiz.Controls.Add(l6); Tr(l6, "آخر 6 أشهر:", "Last 6 months:");
            bzMonths = new TextBox { Multiline = true, ReadOnly = true, RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(6, 9, 15), ForeColor = Color.FromArgb(120, 190, 255), Location = new Point(20, 250), Size = new Size(330, 150), Font = new Font("Consolas", 9), BorderStyle = BorderStyle.FixedSingle };
            pageBiz.Controls.Add(bzMonths);

            // بحث سريع موحّد
            var sl = new Label { Text = "🔎 بحث سريع (زبون / هاتف / آيمي / جهاز):", Location = new Point(20, 408), Size = new Size(675, 22), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 10, FontStyle.Bold) };
            pageBiz.Controls.Add(sl); Tr(sl, "🔎 بحث سريع (زبون / هاتف / آيمي / جهاز):", "🔎 Quick search (customer / phone / IMEI / device):");
            bzSearchBox = new TextBox { Location = new Point(20, 434), Size = new Size(560, 28), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 10) };
            bzSearchBox.TextChanged += (s, e) => BizSearch();
            pageBiz.Controls.Add(bzSearchBox);
            var sb = new Button { Text = "بحث", Location = new Point(585, 433), Size = new Size(110, 30), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            sb.FlatAppearance.BorderSize = 0; sb.Click += (s, e) => BizSearch();
            pageBiz.Controls.Add(sb); Tr(sb, "بحث", "Search");

            bzResults = new ListBox { Location = new Point(20, 468), Size = new Size(675, 174), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            pageBiz.Controls.Add(bzResults);
        }

        Label BizTile(Panel parent, int x, string icon, string cap, Color col)
        {
            Color cardBg = Color.FromArgb(22, 32, 60);
            var card = new Panel { Location = new Point(x, 50), Size = new Size(165, 92), BackColor = cardBg };
            try { using (var gp = RRect(new Rectangle(0, 0, card.Width, card.Height), 14)) card.Region = new Region(gp); } catch { }
            var accent = new Panel { Location = new Point(14, 8), Size = new Size(137, 4), BackColor = col };
            var ic = new Label { Text = icon, ForeColor = col, BackColor = cardBg, Location = new Point(0, 14), Size = new Size(165, 20), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Emoji", 11) };
            var val = new Label { Text = "0", ForeColor = Color.White, BackColor = cardBg, Location = new Point(0, 34), Size = new Size(165, 32), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 16, FontStyle.Bold) };
            var lab = new Label { Text = cap, ForeColor = Color.FromArgb(170, 190, 220), BackColor = cardBg, Location = new Point(0, 68), Size = new Size(165, 18), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 8, FontStyle.Bold) };
            card.Controls.Add(accent); card.Controls.Add(ic); card.Controls.Add(val); card.Controls.Add(lab);
            parent.Controls.Add(card);
            return val;
        }

        // تحويل نص «Created» إلى تاريخ (yyyy-MM-dd HH:mm) بأمان
        static DateTime? BizDate(string created)
        {
            if (string.IsNullOrWhiteSpace(created)) return null;
            DateTime d;
            if (DateTime.TryParse(created.Trim(), out d)) return d;
            // محاولة أخذ أول 10 أحرف (yyyy-MM-dd)
            var s = created.Trim();
            if (s.Length >= 10 && DateTime.TryParse(s.Substring(0, 10), out d)) return d;
            return null;
        }

        void BizLoad()
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                double tDay = 0, tMonth = 0, tYear = 0, tAll = 0, labSum = 0, itemsSum = 0, maxInv = 0;
                int cnt = 0;
                foreach (var v in invoiceList)
                {
                    double tot = v.Total;
                    tAll += tot; cnt++;
                    if (tot > maxInv) maxInv = tot;
                    labSum += v.Labor;
                    if (v.Items != null) foreach (var it in v.Items) itemsSum += it.Qty * it.Price;
                    var d = BizDate(v.Created);
                    if (d.HasValue)
                    {
                        if (d.Value.Date == now.Date) tDay += tot;
                        if (d.Value.Year == now.Year && d.Value.Month == now.Month) tMonth += tot;
                        if (d.Value.Year == now.Year) tYear += tot;
                    }
                }
                if (bzToday != null) bzToday.Text = Money(tDay);
                if (bzMonth != null) bzMonth.Text = Money(tMonth);
                if (bzYear != null) bzYear.Text = Money(tYear);
                if (bzTotal != null) bzTotal.Text = Money(tAll);

                double avg = cnt > 0 ? tAll / cnt : 0;
                int low = 0; try { low = LowStockCount(); } catch { }
                string lowTxt = low > 0 ? "   |   ⚠ مخزون منخفض: " + low + " قطعة" : "";
                if (bzSummary != null)
                    bzSummary.Text =
                        "عدد الفواتير: " + cnt + "   |   متوسط الفاتورة: " + Money(avg) + "   |   أعلى فاتورة: " + Money(maxInv) + "\n" +
                        "مجموع الأجور (ربح الصيانة): " + Money(labSum) + "   |   مبيعات القطع: " + Money(itemsSum) + lowTxt;

                BizWeek(now); BizSixMonths(now);
                BizSearch();
            }
            catch (Exception ex) { try { ErrBox("تعذّر حساب التقارير: " + ex.Message); } catch { } }
        }

        // تفصيل آخر 7 أيام بأعمدة نصية
        void BizWeek(DateTime now)
        {
            var map = new Dictionary<string, double>();
            for (int i = 6; i >= 0; i--) map[now.Date.AddDays(-i).ToString("MM-dd")] = 0;
            foreach (var v in invoiceList)
            {
                var d = BizDate(v.Created); if (!d.HasValue) continue;
                string k = d.Value.ToString("MM-dd");
                if (map.ContainsKey(k)) map[k] += v.Total;
            }
            double mx = map.Values.Count > 0 ? Math.Max(1, map.Values.Max()) : 1;
            var sb = new StringBuilder();
            foreach (var kv in map)
            {
                int bars = (int)Math.Round(kv.Value / mx * 12);
                sb.AppendLine(kv.Key + " " + new string('█', bars).PadRight(12, '·') + " " + Money(kv.Value));
            }
            if (bzWeek != null) bzWeek.Text = sb.ToString();
        }

        void BizSixMonths(DateTime now)
        {
            var map = new List<KeyValuePair<string, double>>();
            var keys = new List<string>();
            for (int i = 5; i >= 0; i--) { var m = now.AddMonths(-i); keys.Add(m.ToString("yyyy-MM")); map.Add(new KeyValuePair<string, double>(m.ToString("yyyy-MM"), 0)); }
            var dict = keys.ToDictionary(k => k, k => 0.0);
            foreach (var v in invoiceList)
            {
                var d = BizDate(v.Created); if (!d.HasValue) continue;
                string k = d.Value.ToString("yyyy-MM");
                if (dict.ContainsKey(k)) dict[k] += v.Total;
            }
            double mx = dict.Values.Count > 0 ? Math.Max(1, dict.Values.Max()) : 1;
            var sb = new StringBuilder();
            foreach (var k in keys)
            {
                int bars = (int)Math.Round(dict[k] / mx * 12);
                sb.AppendLine(k + " " + new string('█', bars).PadRight(12, '·') + " " + Money(dict[k]));
            }
            if (bzMonths != null) bzMonths.Text = sb.ToString();
        }

        static bool Has(string hay, string needle)
        {
            if (string.IsNullOrEmpty(needle)) return false;
            return (hay ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // بحث موحّد عبر الزبائن + الفواتير + التذاكر
        void BizSearch()
        {
            if (bzResults == null) return;
            string q = (bzSearchBox != null ? bzSearchBox.Text : "").Trim();
            bzResults.Items.Clear();
            try
            {
                if (q == "")
                {
                    // لا بحث: اعرض المخزون المنخفض + آخر الفواتير
                    var low = partList.Where(p => p.Qty <= p.MinQty).ToList();
                    if (low.Count > 0)
                    {
                        bzResults.Items.Add("⚠ قطع تحت الحد الأدنى:");
                        foreach (var p in low) bzResults.Items.Add("   📦 " + (p.Name ?? "") + " — كمية " + p.Qty + " (حد " + p.MinQty + ")");
                        bzResults.Items.Add("");
                    }
                    bzResults.Items.Add("🧾 آخر الفواتير:");
                    foreach (var v in invoiceList.Take(12))
                        bzResults.Items.Add("   #" + (v.Id ?? "") + " | " + (v.Customer ?? "") + " | " + (v.Device ?? "") + " | " + Money(v.Total) + " | " + (v.Created ?? ""));
                    return;
                }

                int hits = 0;
                var cust = customerList.Where(c => Has(c.Name, q) || Has(c.Phone, q) || Has(c.Note, q)).ToList();
                if (cust.Count > 0) { bzResults.Items.Add("👥 زبائن (" + cust.Count + "):"); foreach (var c in cust) { bzResults.Items.Add("   " + (c.Name ?? "") + " | " + (c.Phone ?? "") + (string.IsNullOrWhiteSpace(c.Note) ? "" : " | " + c.Note)); hits++; } }

                var inv = invoiceList.Where(v => Has(v.Customer, q) || Has(v.Phone, q) || Has(v.Device, q) || Has(v.Id, q)).ToList();
                if (inv.Count > 0) { bzResults.Items.Add("🧾 فواتير (" + inv.Count + "):"); foreach (var v in inv) { bzResults.Items.Add("   #" + (v.Id ?? "") + " | " + (v.Customer ?? "") + " | " + (v.Device ?? "") + " | " + Money(v.Total) + " | " + (v.Created ?? "")); hits++; } }

                var tk = ticketList.Where(t => Has(t.Customer, q) || Has(t.Phone, q) || Has(t.Device, q) || Has(t.Issue, q) || Has(t.Id, q)).ToList();
                if (tk.Count > 0) { bzResults.Items.Add("🎫 تذاكر (" + tk.Count + "):"); foreach (var t in tk) { bzResults.Items.Add("   #" + (t.Id ?? "") + " | " + (t.Customer ?? "") + " | " + (t.Device ?? "") + " | " + (t.Status ?? "") + " | " + (t.Issue ?? "")); hits++; } }

                if (hits == 0) bzResults.Items.Add("لا نتائج لـ: " + q);
            }
            catch (Exception ex) { bzResults.Items.Add("خطأ بالبحث: " + ex.Message); }
        }

        static string Money(double v) { return v.ToString("#,0.##") + " د.ع"; }

        // تصدير تقرير مالي HTML وفتحه للطباعة
        void BizExport()
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                double tDay = 0, tMonth = 0, tYear = 0, tAll = 0, labSum = 0, itemsSum = 0, maxInv = 0; int cnt = 0;
                foreach (var v in invoiceList)
                {
                    tAll += v.Total; cnt++; if (v.Total > maxInv) maxInv = v.Total; labSum += v.Labor;
                    if (v.Items != null) foreach (var it in v.Items) itemsSum += it.Qty * it.Price;
                    var d = BizDate(v.Created);
                    if (d.HasValue) { if (d.Value.Date == now.Date) tDay += v.Total; if (d.Value.Year == now.Year && d.Value.Month == now.Month) tMonth += v.Total; if (d.Value.Year == now.Year) tYear += v.Total; }
                }
                double avg = cnt > 0 ? tAll / cnt : 0;
                var sb = new StringBuilder();
                sb.Append("<!DOCTYPE html><html dir='rtl' lang='ar'><head><meta charset='utf-8'><title>تقرير مالي</title>");
                sb.Append("<style>body{font-family:Arial;background:#fff;color:#111;padding:24px}h1{color:#1a66cc;margin:0}");
                sb.Append(".h{border-bottom:3px solid #1a66cc;padding-bottom:10px;margin-bottom:14px}.muted{color:#666;font-size:13px}");
                sb.Append("table{width:100%;border-collapse:collapse;margin-top:12px}th,td{border:1px solid #ccc;padding:8px;text-align:right}th{background:#eef3fb}");
                sb.Append(".big{font-size:20px;font-weight:bold;color:#0a8a3a}</style></head><body>");
                sb.Append("<div class='h'><h1>سنتر بوينت للاتصالات</h1><div class='muted'>تقرير مالي — " + now.ToString("yyyy-MM-dd HH:mm") + "</div></div>");
                sb.Append("<table><tr><th>البند</th><th>القيمة</th></tr>");
                sb.Append("<tr><td>إيراد اليوم</td><td>" + Money(tDay) + "</td></tr>");
                sb.Append("<tr><td>إيراد الشهر</td><td>" + Money(tMonth) + "</td></tr>");
                sb.Append("<tr><td>إيراد السنة</td><td>" + Money(tYear) + "</td></tr>");
                sb.Append("<tr><td>الإجمالي الكلي</td><td class='big'>" + Money(tAll) + "</td></tr>");
                sb.Append("<tr><td>عدد الفواتير</td><td>" + cnt + "</td></tr>");
                sb.Append("<tr><td>متوسط الفاتورة</td><td>" + Money(avg) + "</td></tr>");
                sb.Append("<tr><td>أعلى فاتورة</td><td>" + Money(maxInv) + "</td></tr>");
                sb.Append("<tr><td>مجموع الأجور (ربح الصيانة)</td><td>" + Money(labSum) + "</td></tr>");
                sb.Append("<tr><td>مبيعات القطع</td><td>" + Money(itemsSum) + "</td></tr>");
                sb.Append("</table>");
                sb.Append("<p class='muted' style='margin-top:20px'>ملاحظة: «الإيراد» هو مجموع الفواتير. «ربح الصيانة» تقديري من الأجور (لا يشمل كلفة القطع).</p>");
                sb.Append("</body></html>");
                string path = Path.Combine(ExportsDir, "financial_report_" + now.ToString("yyMMddHHmmss") + ".html");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                LogOp("تصدير تقرير مالي");
            }
            catch (Exception ex) { ErrBox("تعذّر تصدير التقرير: " + ex.Message); }
        }
    }
}
