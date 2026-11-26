// -*- coding: utf-8 -*-
// Center Point - قسم الفواتير (Invoices) — partial class + تخزين JSON
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Text.Json;

namespace CenterPoint
{
    // سطر في الفاتورة
    public class InvItem
    {
        public string Desc { get; set; }
        public int Qty { get; set; }
        public double Price { get; set; }
        public string PartId { get; set; }   // ربط ببند مخزون (اختياري) لخصم الكمية
    }

    // نموذج فاتورة
    public class Invoice
    {
        public string Id { get; set; }
        public string Customer { get; set; }
        public string Phone { get; set; }
        public string Device { get; set; }
        public List<InvItem> Items { get; set; }
        public double Labor { get; set; }
        public double Total { get; set; }
        public string Created { get; set; }
    }

    public partial class MainForm
    {
        Panel pageInvoice; Button biInvoice;
        TextBox ivCustomer, ivPhone, ivDevice, ivItemDesc, ivItemQty, ivItemPrice, ivLabor;
        ListBox ivItemsList, ivList; Label ivTotalLbl;
        List<InvItem> ivCurrentItems = new List<InvItem>();
        List<Invoice> invoiceList = new List<Invoice>(); string ivEditId = "";

        string InvoicesPath { get => Path.Combine(DataDir, "invoices.json"); }

        void BuildInvoicesPage()
        {
            pageInvoice = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageInvoice);

            var title = new Label { Text = "🧾 الفواتير", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 10), Size = new Size(675, 26), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageInvoice.Controls.Add(title);
            Tr(title, "🧾 الفواتير", "🧾 Invoices");

            IvLbl(pageInvoice, "الزبون:", 20, 46);
            ivCustomer = IvBox(pageInvoice, 85, 42, 158);
            var ivCustPick = new Button { Text = "👥", Location = new Point(246, 42), Size = new Size(42, 28), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 11, FontStyle.Bold) };
            ivCustPick.FlatAppearance.BorderSize = 0; ivCustPick.Click += (s, e) => IvPickCustomer();
            pageInvoice.Controls.Add(ivCustPick);
            IvLbl(pageInvoice, "الهاتف:", 295, 46);
            ivPhone = IvBox(pageInvoice, 355, 42, 160);
            IvLbl(pageInvoice, "الجهاز:", 525, 46);
            ivDevice = IvBox(pageInvoice, 590, 42, 105);

            // سطر إضافة بند
            IvLbl(pageInvoice, "بند:", 20, 82);
            ivItemDesc = IvBox(pageInvoice, 85, 78, 250);
            IvLbl(pageInvoice, "كمية:", 345, 82);
            ivItemQty = IvBox(pageInvoice, 400, 78, 55); ivItemQty.Text = "1";
            IvLbl(pageInvoice, "سعر:", 465, 82);
            ivItemPrice = IvBox(pageInvoice, 515, 78, 90); ivItemPrice.Text = "0";
            var addItem = new Button { Text = "➕ بند", Location = new Point(613, 77), Size = new Size(82, 28), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            addItem.FlatAppearance.BorderSize = 0; addItem.Click += (s, e) => IvAddItem();
            pageInvoice.Controls.Add(addItem);

            ivItemsList = new ListBox { Location = new Point(20, 114), Size = new Size(675, 150), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            pageInvoice.Controls.Add(ivItemsList);
            var rmItem = new Button { Text = "🗑 حذف البند المحدّد", Location = new Point(20, 270), Size = new Size(190, 28), BackColor = Color.FromArgb(150, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            rmItem.FlatAppearance.BorderSize = 0; rmItem.Click += (s, e) => IvRemoveItem();
            pageInvoice.Controls.Add(rmItem);
            var stockItem = new Button { Text = "📦 من المخزون", Location = new Point(216, 270), Size = new Size(125, 28), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            stockItem.FlatAppearance.BorderSize = 0; stockItem.Click += (s, e) => IvAddFromStock();
            pageInvoice.Controls.Add(stockItem);

            IvLbl(pageInvoice, "أجور:", 345, 274);
            ivLabor = IvBox(pageInvoice, 400, 270, 90); ivLabor.Text = "0";
            ivLabor.TextChanged += (s, e) => IvRecalcTotal();
            ivTotalLbl = new Label { Text = "الإجمالي: 0", Location = new Point(500, 270), Size = new Size(195, 28), ForeColor = Color.FromArgb(34, 195, 94), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 12, FontStyle.Bold) };
            pageInvoice.Controls.Add(ivTotalLbl);

            // أزرار الفاتورة
            var saveBtn = new Button { Text = "💾 حفظ الفاتورة", Location = new Point(20, 306), Size = new Size(160, 32), BackColor = Color.FromArgb(34, 160, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            saveBtn.FlatAppearance.BorderSize = 0; saveBtn.Click += (s, e) => IvSave();
            var printBtn = new Button { Text = "🖨 طباعة / تصدير", Location = new Point(188, 306), Size = new Size(160, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            printBtn.FlatAppearance.BorderSize = 0; printBtn.Click += (s, e) => IvPrint();
            var delBtn = new Button { Text = "🗑 حذف", Location = new Point(356, 306), Size = new Size(110, 32), BackColor = Color.FromArgb(150, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            delBtn.FlatAppearance.BorderSize = 0; delBtn.Click += (s, e) => IvDelete();
            var clrBtn = new Button { Text = "🧹 جديدة", Location = new Point(474, 306), Size = new Size(120, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            clrBtn.FlatAppearance.BorderColor = BLUE; clrBtn.Click += (s, e) => IvClear();
            pageInvoice.Controls.Add(saveBtn); pageInvoice.Controls.Add(printBtn); pageInvoice.Controls.Add(delBtn); pageInvoice.Controls.Add(clrBtn);

            var savedLbl = new Label { Text = "الفواتير المحفوظة:", Location = new Point(20, 348), Size = new Size(675, 20), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 10, FontStyle.Bold) };
            pageInvoice.Controls.Add(savedLbl);
            ivList = new ListBox { Location = new Point(20, 372), Size = new Size(675, 266), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            ivList.SelectedIndexChanged += (s, e) => IvSelect();
            pageInvoice.Controls.Add(ivList);

            InvoicesLoad();
        }

        void IvLbl(Panel p, string text, int x, int y)
        {
            p.Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(62, 20), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold) });
        }

        TextBox IvBox(Panel p, int x, int y, int w)
        {
            var t = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 10) };
            p.Controls.Add(t); return t;
        }

        double IvItemsSum() { double s = 0; foreach (var it in ivCurrentItems) s += it.Qty * it.Price; return s; }

        void IvRecalcTotal()
        {
            double total = IvItemsSum() + ParseD(ivLabor.Text);
            if (ivTotalLbl != null) ivTotalLbl.Text = "الإجمالي: " + total.ToString("0.##");
        }

        void IvRefreshItems()
        {
            if (ivItemsList == null) return;
            ivItemsList.Items.Clear();
            foreach (var it in ivCurrentItems)
                ivItemsList.Items.Add((it.Desc ?? "") + "  ×" + it.Qty + "  @ " + it.Price.ToString("0.##") + "  = " + (it.Qty * it.Price).ToString("0.##"));
            IvRecalcTotal();
        }

        void IvAddItem()
        {
            string desc = (ivItemDesc.Text ?? "").Trim();
            if (desc == "") { MessageBox.Show("اكتب وصف البند.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            ivCurrentItems.Add(new InvItem { Desc = desc, Qty = Math.Max(1, ParseI(ivItemQty.Text)), Price = ParseD(ivItemPrice.Text) });
            ivItemDesc.Text = ""; ivItemQty.Text = "1"; ivItemPrice.Text = "0";
            IvRefreshItems();
        }

        void IvRemoveItem()
        {
            int i = ivItemsList.SelectedIndex;
            if (i < 0 || i >= ivCurrentItems.Count) return;
            ivCurrentItems.RemoveAt(i);
            IvRefreshItems();
        }

        void InvoicesLoad()
        {
            try
            {
                if (File.Exists(InvoicesPath))
                    invoiceList = JsonSerializer.Deserialize<List<Invoice>>(File.ReadAllText(InvoicesPath)) ?? new List<Invoice>();
                else invoiceList = new List<Invoice>();
            }
            catch { invoiceList = new List<Invoice>(); }
            InvoicesRefreshList();
        }

        void InvoicesSave()
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(InvoicesPath, JsonSerializer.Serialize(invoiceList, opt), new UTF8Encoding(true));
            }
            catch (Exception ex) { ErrBox("تعذّر حفظ الفواتير: " + ex.Message); }
        }

        void InvoicesRefreshList()
        {
            if (ivList == null) return;
            ivList.Items.Clear();
            foreach (var v in invoiceList)
                ivList.Items.Add("#" + (v.Id ?? "") + "  |  " + (v.Customer ?? "") + "  |  " + (v.Device ?? "") + "  |  " + v.Total.ToString("0.##") + "  |  " + (v.Created ?? ""));
        }

        void IvSave()
        {
            string cust = (ivCustomer.Text ?? "").Trim();
            if (cust == "") { MessageBox.Show("اكتب اسم الزبون.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (ivCurrentItems.Count == 0) { MessageBox.Show("أضف بنداً واحداً على الأقل.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            double total = IvItemsSum() + ParseD(ivLabor.Text);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            if (ivEditId != "")
            {
                var v = invoiceList.FirstOrDefault(x => x.Id == ivEditId);
                if (v != null)
                {
                    v.Customer = cust; v.Phone = ivPhone.Text.Trim(); v.Device = ivDevice.Text.Trim();
                    v.Items = new List<InvItem>(ivCurrentItems); v.Labor = ParseD(ivLabor.Text); v.Total = total;
                }
            }
            else
            {
                invoiceList.Insert(0, new Invoice { Id = DateTime.Now.ToString("yyMMddHHmmss"), Customer = cust, Phone = ivPhone.Text.Trim(), Device = ivDevice.Text.Trim(), Items = new List<InvItem>(ivCurrentItems), Labor = ParseD(ivLabor.Text), Total = total, Created = now });
                IvDeductStock();   // خصم قطع المخزون — للفواتير الجديدة فقط
            }
            InvoicesSave(); InvoicesRefreshList(); IvClear();
            LogOp("فاتورة: " + cust + " = " + total.ToString("0.##"));
        }

        void IvDelete()
        {
            int i = ivList.SelectedIndex;
            if (i < 0 || i >= invoiceList.Count) { MessageBox.Show("اختر فاتورة أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (MessageBox.Show("حذف فاتورة: " + invoiceList[i].Customer + "؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            invoiceList.RemoveAt(i);
            InvoicesSave(); InvoicesRefreshList(); IvClear();
        }

        void IvClear()
        {
            ivEditId = "";
            ivCurrentItems = new List<InvItem>();
            if (ivCustomer != null) ivCustomer.Text = "";
            if (ivPhone != null) ivPhone.Text = "";
            if (ivDevice != null) ivDevice.Text = "";
            if (ivLabor != null) ivLabor.Text = "0";
            if (ivList != null) ivList.ClearSelected();
            IvRefreshItems();
        }

        void IvSelect()
        {
            int i = ivList.SelectedIndex;
            if (i < 0 || i >= invoiceList.Count) return;
            var v = invoiceList[i];
            ivEditId = v.Id;
            ivCustomer.Text = v.Customer ?? ""; ivPhone.Text = v.Phone ?? ""; ivDevice.Text = v.Device ?? "";
            ivLabor.Text = v.Labor.ToString("0.##");
            ivCurrentItems = v.Items != null ? new List<InvItem>(v.Items) : new List<InvItem>();
            IvRefreshItems();
        }

        // تصدير الفاتورة كملف HTML وفتحها للطباعة
        void IvPrint()
        {
            string cust = (ivCustomer.Text ?? "").Trim();
            if (cust == "" || ivCurrentItems.Count == 0) { MessageBox.Show("أدخل زبوناً وبنداً واحداً على الأقل.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            double itemsSum = IvItemsSum(); double labor = ParseD(ivLabor.Text); double total = itemsSum + labor;
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html dir='rtl' lang='ar'><head><meta charset='utf-8'><title>فاتورة</title>");
            sb.Append("<style>body{font-family:Arial;background:#fff;color:#111;padding:24px}h1{color:#1a66cc;margin:0}");
            sb.Append(".h{display:flex;justify-content:space-between;border-bottom:3px solid #1a66cc;padding-bottom:10px}");
            sb.Append("table{width:100%;border-collapse:collapse;margin-top:16px}th,td{border:1px solid #ccc;padding:8px;text-align:right}");
            sb.Append("th{background:#eef3fb}.tot{font-size:18px;font-weight:bold;color:#0a8a3a;margin-top:14px;text-align:left}");
            sb.Append(".muted{color:#666;font-size:13px}</style></head><body>");
            sb.Append("<div class='h'><div><h1>سنتر بوينت للاتصالات</h1><div class='muted'>Center Point — فاتورة صيانة</div></div>");
            sb.Append("<div class='muted'>التاريخ: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "<br>هاتف المحل: 0782 915 5923</div></div>");
            sb.Append("<p><b>الزبون:</b> " + Esc(cust) + " &nbsp; <b>الهاتف:</b> " + Esc(ivPhone.Text.Trim()) + " &nbsp; <b>الجهاز:</b> " + Esc(ivDevice.Text.Trim()) + "</p>");
            sb.Append("<table><tr><th>البند</th><th>الكمية</th><th>السعر</th><th>المجموع</th></tr>");
            foreach (var it in ivCurrentItems)
                sb.Append("<tr><td>" + Esc(it.Desc) + "</td><td>" + it.Qty + "</td><td>" + it.Price.ToString("0.##") + "</td><td>" + (it.Qty * it.Price).ToString("0.##") + "</td></tr>");
            sb.Append("</table>");
            sb.Append("<div class='tot'>مجموع القطع: " + itemsSum.ToString("0.##") + " &nbsp;|&nbsp; الأجور: " + labor.ToString("0.##") + "</div>");
            sb.Append("<div class='tot'>الإجمالي للدفع: " + total.ToString("0.##") + "</div>");
            sb.Append("<p class='muted' style='margin-top:24px'>شكراً لثقتكم — سنتر بوينت للاتصالات</p>");
            sb.Append("</body></html>");
            try
            {
                string path = Path.Combine(ExportsDir, "invoice_" + DateTime.Now.ToString("yyMMddHHmmss") + ".html");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                LogOp("طباعة فاتورة: " + cust);
            }
            catch (Exception ex) { ErrBox("تعذّر إنشاء ملف الفاتورة: " + ex.Message); }
        }

        static string Esc(string s)
        {
            s = s ?? "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
