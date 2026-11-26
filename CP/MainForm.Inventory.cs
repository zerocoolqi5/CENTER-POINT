// -*- coding: utf-8 -*-
// Center Point - قسم المخزون / قطع الغيار (Inventory) — partial class + تخزين JSON
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Text.Json;

namespace CenterPoint
{
    // نموذج قطعة غيار
    public class Part
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public int Qty { get; set; }
        public int MinQty { get; set; }
        public double Price { get; set; }
        public string Note { get; set; }
    }

    public partial class MainForm
    {
        Panel pageInv; Button biInv;
        TextBox ptName, ptSku, ptQty, ptMin, ptPrice, ptNote; ListBox ptList;
        List<Part> partList = new List<Part>(); string ptEditId = "";

        string InventoryPath { get => Path.Combine(DataDir, "inventory.json"); }

        void BuildInventoryPage()
        {
            pageInv = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageInv);

            var title = new Label { Text = "📦 المخزون — قطع الغيار", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageInv.Controls.Add(title);
            Tr(title, "📦 المخزون — قطع الغيار", "📦 Inventory — Spare Parts");

            InLbl(pageInv, "القطعة:", 20, 54);
            ptName = InBox(pageInv, 85, 50, 250);
            InLbl(pageInv, "الكود:", 345, 54);
            ptSku = InBox(pageInv, 405, 50, 290);
            InLbl(pageInv, "الكمية:", 20, 90);
            ptQty = InBox(pageInv, 85, 86, 100); ptQty.Text = "0";
            InLbl(pageInv, "حد أدنى:", 195, 90);
            ptMin = InBox(pageInv, 260, 86, 75); ptMin.Text = "1";
            InLbl(pageInv, "السعر:", 345, 90);
            ptPrice = InBox(pageInv, 405, 86, 120); ptPrice.Text = "0";
            InLbl(pageInv, "ملاحظة:", 20, 126);
            ptNote = InBox(pageInv, 85, 122, 610);

            var saveBtn = new Button { Text = "➕ حفظ / تحديث", Location = new Point(20, 162), Size = new Size(160, 32), BackColor = Color.FromArgb(34, 160, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            saveBtn.FlatAppearance.BorderSize = 0; saveBtn.Click += (s, e) => InAddOrUpdate();
            var inBtn = new Button { Text = "➕1 إدخال", Location = new Point(188, 162), Size = new Size(95, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            inBtn.FlatAppearance.BorderSize = 0; inBtn.Click += (s, e) => InAdjust(1);
            var outBtn = new Button { Text = "➖1 صرف", Location = new Point(291, 162), Size = new Size(95, 32), BackColor = Color.FromArgb(150, 90, 20), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            outBtn.FlatAppearance.BorderSize = 0; outBtn.Click += (s, e) => InAdjust(-1);
            var delBtn = new Button { Text = "🗑 حذف", Location = new Point(394, 162), Size = new Size(95, 32), BackColor = Color.FromArgb(150, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            delBtn.FlatAppearance.BorderSize = 0; delBtn.Click += (s, e) => InDelete();
            var clrBtn = new Button { Text = "🧹 مسح", Location = new Point(497, 162), Size = new Size(95, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            clrBtn.FlatAppearance.BorderColor = BLUE; clrBtn.Click += (s, e) => InClear();
            pageInv.Controls.Add(saveBtn); pageInv.Controls.Add(inBtn); pageInv.Controls.Add(outBtn); pageInv.Controls.Add(delBtn); pageInv.Controls.Add(clrBtn);

            ptList = new ListBox { Location = new Point(20, 206), Size = new Size(675, 432), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            ptList.SelectedIndexChanged += (s, e) => InSelect();
            pageInv.Controls.Add(ptList);

            InventoryLoad();
        }

        void InLbl(Panel p, string text, int x, int y)
        {
            p.Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(62, 20), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold) });
        }

        TextBox InBox(Panel p, int x, int y, int w)
        {
            var t = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 10) };
            p.Controls.Add(t); return t;
        }

        void InventoryLoad()
        {
            try
            {
                if (File.Exists(InventoryPath))
                    partList = JsonSerializer.Deserialize<List<Part>>(File.ReadAllText(InventoryPath)) ?? new List<Part>();
                else partList = new List<Part>();
            }
            catch { partList = new List<Part>(); }
            InventoryRefreshList();
        }

        void InventorySave()
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(InventoryPath, JsonSerializer.Serialize(partList, opt), new UTF8Encoding(true));
            }
            catch (Exception ex) { ErrBox("تعذّر حفظ المخزون: " + ex.Message); }
        }

        void InventoryRefreshList()
        {
            if (ptList == null) return;
            ptList.Items.Clear();
            foreach (var p in partList)
            {
                string warn = (p.Qty <= p.MinQty) ? "  ⚠ منخفض" : "";
                ptList.Items.Add((p.Name ?? "") + "  |  كود " + (p.Sku ?? "-") + "  |  كمية " + p.Qty + "  |  " + p.Price.ToString("0.##") + warn);
            }
        }

        int LowStockCount() { try { return partList.Count(p => p.Qty <= p.MinQty); } catch { return 0; } }

        static int ParseI(string s) { int v; return int.TryParse((s ?? "").Trim(), out v) ? v : 0; }
        static double ParseD(string s) { double v; return double.TryParse((s ?? "").Trim(), out v) ? v : 0; }

        void InAddOrUpdate()
        {
            string name = (ptName.Text ?? "").Trim();
            if (name == "") { MessageBox.Show("اكتب اسم القطعة.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (ptEditId != "")
            {
                var p = partList.FirstOrDefault(x => x.Id == ptEditId);
                if (p != null) { p.Name = name; p.Sku = ptSku.Text.Trim(); p.Qty = ParseI(ptQty.Text); p.MinQty = ParseI(ptMin.Text); p.Price = ParseD(ptPrice.Text); p.Note = ptNote.Text.Trim(); }
            }
            else
            {
                partList.Insert(0, new Part { Id = DateTime.Now.ToString("yyMMddHHmmss"), Name = name, Sku = ptSku.Text.Trim(), Qty = ParseI(ptQty.Text), MinQty = ParseI(ptMin.Text), Price = ParseD(ptPrice.Text), Note = ptNote.Text.Trim() });
            }
            InventorySave(); InventoryRefreshList(); InClear();
            LogOp("مخزون: " + name);
        }

        // إدخال/صرف قطعة من المحدّدة
        void InAdjust(int delta)
        {
            int i = ptList.SelectedIndex;
            if (i < 0 || i >= partList.Count) { MessageBox.Show("اختر قطعة أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            partList[i].Qty = Math.Max(0, partList[i].Qty + delta);
            InventorySave(); InventoryRefreshList();
            if (partList[i].Qty <= partList[i].MinQty)
                MessageBox.Show("⚠ الكمية وصلت الحد الأدنى: " + partList[i].Name + " (" + partList[i].Qty + ")", "تنبيه مخزون", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void InDelete()
        {
            int i = ptList.SelectedIndex;
            if (i < 0 || i >= partList.Count) { MessageBox.Show("اختر قطعة أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (MessageBox.Show("حذف القطعة: " + partList[i].Name + "؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            partList.RemoveAt(i);
            InventorySave(); InventoryRefreshList(); InClear();
        }

        void InClear()
        {
            ptEditId = "";
            if (ptName != null) ptName.Text = "";
            if (ptSku != null) ptSku.Text = "";
            if (ptQty != null) ptQty.Text = "0";
            if (ptMin != null) ptMin.Text = "1";
            if (ptPrice != null) ptPrice.Text = "0";
            if (ptNote != null) ptNote.Text = "";
            if (ptList != null) ptList.ClearSelected();
        }

        void InSelect()
        {
            int i = ptList.SelectedIndex;
            if (i < 0 || i >= partList.Count) return;
            var p = partList[i];
            ptEditId = p.Id;
            ptName.Text = p.Name ?? ""; ptSku.Text = p.Sku ?? ""; ptQty.Text = p.Qty.ToString();
            ptMin.Text = p.MinQty.ToString(); ptPrice.Text = p.Price.ToString("0.##"); ptNote.Text = p.Note ?? "";
        }
    }
}
