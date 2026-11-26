// -*- coding: utf-8 -*-
// Center Point - ربط الأقسام: التذكرة↔الزبون/الفاتورة، الفاتورة↔الزبون/المخزون — partial class
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

namespace CenterPoint
{
    public partial class MainForm
    {
        // ===== مُنتقي عام من قائمة (نافذة) =====
        int PickIndex(string title, List<string> items)
        {
            if (items == null || items.Count == 0) return -1;
            int result = -1;
            using (var f = new Form())
            {
                f.Text = title; f.StartPosition = FormStartPosition.CenterParent;
                f.Size = new Size(470, 430); f.BackColor = BG; f.ForeColor = Color.White;
                f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MaximizeBox = false; f.MinimizeBox = false;
                f.RightToLeft = RightToLeft.Yes; f.ShowInTaskbar = false;

                var lb = new ListBox { Location = new Point(14, 14), Size = new Size(424, 326), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 10), RightToLeft = RightToLeft.Yes };
                foreach (var it in items) lb.Items.Add(it);
                if (lb.Items.Count > 0) lb.SelectedIndex = 0;
                f.Controls.Add(lb);

                var ok = new Button { Text = "اختيار", Location = new Point(248, 350), Size = new Size(190, 36), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
                ok.FlatAppearance.BorderSize = 0;
                ok.Click += (s, e) => { result = lb.SelectedIndex; f.Close(); };
                lb.DoubleClick += (s, e) => { result = lb.SelectedIndex; f.Close(); };
                var cancel = new Button { Text = "إلغاء", Location = new Point(14, 350), Size = new Size(130, 36), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 10, FontStyle.Bold) };
                cancel.FlatAppearance.BorderColor = BLUE; cancel.Click += (s, e) => { result = -1; f.Close(); };
                f.Controls.Add(ok); f.Controls.Add(cancel);

                f.ShowDialog(this);
            }
            return result;
        }

        Customer PickCustomer()
        {
            if (customerList == null || customerList.Count == 0)
            {
                MessageBox.Show("لا يوجد زبائن محفوظين. أضفهم من قسم «الزبائن» أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            int i = PickIndex("اختر زبون", customerList.Select(c => (c.Name ?? "") + "   |   " + (c.Phone ?? "")).ToList());
            return (i >= 0 && i < customerList.Count) ? customerList[i] : null;
        }

        Part PickPart()
        {
            if (partList == null || partList.Count == 0)
            {
                MessageBox.Show("لا توجد قطع بالمخزون. أضفها من قسم «المخزون» أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            int i = PickIndex("اختر قطعة من المخزون", partList.Select(p => (p.Name ?? "") + "   |   كمية " + p.Qty + "   |   " + p.Price.ToString("0.##")).ToList());
            return (i >= 0 && i < partList.Count) ? partList[i] : null;
        }

        // ===== التذكرة ← الزبون =====
        void TkPickCustomer()
        {
            var c = PickCustomer();
            if (c == null) return;
            if (tkCustomer != null) tkCustomer.Text = c.Name ?? "";
            if (tkPhone != null) tkPhone.Text = c.Phone ?? "";
        }

        // ===== التذكرة → الفاتورة =====
        void TkToInvoice()
        {
            string cust = "", phone = "", dev = "";
            int i = tkList != null ? tkList.SelectedIndex : -1;
            if (i >= 0 && i < ticketList.Count)
            {
                var t = ticketList[i];
                cust = t.Customer ?? ""; phone = t.Phone ?? ""; dev = t.Device ?? "";
            }
            else
            {
                cust = tkCustomer != null ? tkCustomer.Text.Trim() : "";
                phone = tkPhone != null ? tkPhone.Text.Trim() : "";
                dev = tkDevice != null ? tkDevice.Text.Trim() : "";
            }
            if (string.IsNullOrWhiteSpace(cust))
            {
                MessageBox.Show("اختر تذكرة من القائمة أو اكتب زبوناً أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ShowPage("invoices");
            IvClear();
            if (ivCustomer != null) ivCustomer.Text = cust;
            if (ivPhone != null) ivPhone.Text = phone;
            if (ivDevice != null) ivDevice.Text = dev;
            CpLog("OP", "إنشاء فاتورة من تذكرة: " + cust);
        }

        // ===== الفاتورة ← الزبون =====
        void IvPickCustomer()
        {
            var c = PickCustomer();
            if (c == null) return;
            if (ivCustomer != null) ivCustomer.Text = c.Name ?? "";
            if (ivPhone != null) ivPhone.Text = c.Phone ?? "";
        }

        // ===== الفاتورة ← المخزون =====
        void IvAddFromStock()
        {
            var p = PickPart();
            if (p == null) return;
            ivCurrentItems.Add(new InvItem { Desc = p.Name, Qty = 1, Price = p.Price, PartId = p.Id });
            IvRefreshItems();
        }

        // خصم قطع المخزون المرتبطة ببنود الفاتورة (للفواتير الجديدة)
        void IvDeductStock()
        {
            bool changed = false;
            foreach (var it in ivCurrentItems)
            {
                if (string.IsNullOrEmpty(it.PartId)) continue;
                var p = partList.FirstOrDefault(x => x.Id == it.PartId);
                if (p != null)
                {
                    p.Qty = Math.Max(0, p.Qty - Math.Max(1, it.Qty));
                    changed = true;
                    if (p.Qty <= p.MinQty)
                        CpLog("WARN", "مخزون منخفض بعد الفاتورة: " + p.Name + " (" + p.Qty + ")");
                }
            }
            if (changed)
            {
                InventorySave();
                InventoryRefreshList();
            }
        }
    }
}
