// -*- coding: utf-8 -*-
// Center Point - قسم الزبائن (Customers) — partial class + تخزين JSON
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
    // نموذج زبون
    public class Customer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Note { get; set; }
        public string Created { get; set; }
    }

    public partial class MainForm
    {
        Panel pageCust; Button biCust;
        TextBox cuName, cuPhone, cuNote; ListBox cuList;
        List<Customer> customerList = new List<Customer>(); string cuEditId = "";

        string CustomersPath { get => Path.Combine(DataDir, "customers.json"); }

        void BuildCustomersPage()
        {
            pageCust = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageCust);

            var title = new Label { Text = "👥 الزبائن", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageCust.Controls.Add(title);
            Tr(title, "👥 الزبائن", "👥 Customers");

            CuLbl(pageCust, "الاسم:", 20, 54);
            cuName = CuBox(pageCust, 85, 50, 250);
            CuLbl(pageCust, "الهاتف:", 345, 54);
            cuPhone = CuBox(pageCust, 405, 50, 290);
            CuLbl(pageCust, "ملاحظة:", 20, 90);
            cuNote = new TextBox { Location = new Point(85, 86), Size = new Size(610, 48), Multiline = true, BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 10) };
            pageCust.Controls.Add(cuNote);

            var saveBtn = new Button { Text = "➕ حفظ / تحديث", Location = new Point(20, 144), Size = new Size(170, 32), BackColor = Color.FromArgb(34, 160, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            saveBtn.FlatAppearance.BorderSize = 0; saveBtn.Click += (s, e) => CuAddOrUpdate();
            var delBtn = new Button { Text = "🗑 حذف", Location = new Point(198, 144), Size = new Size(110, 32), BackColor = Color.FromArgb(150, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            delBtn.FlatAppearance.BorderSize = 0; delBtn.Click += (s, e) => CuDelete();
            var clrBtn = new Button { Text = "🧹 مسح", Location = new Point(316, 144), Size = new Size(110, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            clrBtn.FlatAppearance.BorderColor = BLUE; clrBtn.Click += (s, e) => CuClear();
            var tkBtn = new Button { Text = "🎫 تذاكر الزبون", Location = new Point(434, 144), Size = new Size(160, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            tkBtn.FlatAppearance.BorderSize = 0; tkBtn.Click += (s, e) => CuShowTickets();
            pageCust.Controls.Add(saveBtn); pageCust.Controls.Add(delBtn); pageCust.Controls.Add(clrBtn); pageCust.Controls.Add(tkBtn);

            cuList = new ListBox { Location = new Point(20, 188), Size = new Size(675, 450), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            cuList.SelectedIndexChanged += (s, e) => CuSelect();
            pageCust.Controls.Add(cuList);

            CustomersLoad();
        }

        void CuLbl(Panel p, string text, int x, int y)
        {
            p.Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(62, 20), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold) });
        }

        TextBox CuBox(Panel p, int x, int y, int w)
        {
            var t = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 10) };
            p.Controls.Add(t); return t;
        }

        void CustomersLoad()
        {
            try
            {
                if (File.Exists(CustomersPath))
                    customerList = JsonSerializer.Deserialize<List<Customer>>(File.ReadAllText(CustomersPath)) ?? new List<Customer>();
                else customerList = new List<Customer>();
            }
            catch { customerList = new List<Customer>(); }
            CustomersRefreshList();
        }

        void CustomersSave()
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(CustomersPath, JsonSerializer.Serialize(customerList, opt), new UTF8Encoding(true));
            }
            catch (Exception ex) { ErrBox("تعذّر حفظ الزبائن: " + ex.Message); }
        }

        void CustomersRefreshList()
        {
            if (cuList == null) return;
            cuList.Items.Clear();
            foreach (var c in customerList)
                cuList.Items.Add((c.Name ?? "") + "  |  " + (c.Phone ?? "") + "  |  " + (c.Created ?? ""));
        }

        int CustomersCount() { try { return customerList.Count; } catch { return 0; } }

        void CuAddOrUpdate()
        {
            string name = (cuName.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("اكتب اسم الزبون.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            if (cuEditId != "")
            {
                var c = customerList.FirstOrDefault(x => x.Id == cuEditId);
                if (c != null) { c.Name = name; c.Phone = cuPhone.Text.Trim(); c.Note = cuNote.Text.Trim(); }
            }
            else
            {
                customerList.Insert(0, new Customer { Id = DateTime.Now.ToString("yyMMddHHmmss"), Name = name, Phone = cuPhone.Text.Trim(), Note = cuNote.Text.Trim(), Created = now });
            }
            CustomersSave(); CustomersRefreshList(); CuClear();
            LogOp("زبون: " + name);
        }

        void CuDelete()
        {
            int i = cuList.SelectedIndex;
            if (i < 0 || i >= customerList.Count) { MessageBox.Show("اختر زبوناً أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (MessageBox.Show("حذف الزبون: " + customerList[i].Name + "؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            customerList.RemoveAt(i);
            CustomersSave(); CustomersRefreshList(); CuClear();
        }

        void CuClear()
        {
            cuEditId = "";
            if (cuName != null) cuName.Text = "";
            if (cuPhone != null) cuPhone.Text = "";
            if (cuNote != null) cuNote.Text = "";
            if (cuList != null) cuList.ClearSelected();
        }

        void CuSelect()
        {
            int i = cuList.SelectedIndex;
            if (i < 0 || i >= customerList.Count) return;
            var c = customerList[i];
            cuEditId = c.Id;
            cuName.Text = c.Name ?? ""; cuPhone.Text = c.Phone ?? ""; cuNote.Text = c.Note ?? "";
        }

        // يعرض تذاكر الصيانة المرتبطة بالزبون (مطابقة بالهاتف أو الاسم)
        void CuShowTickets()
        {
            int i = cuList.SelectedIndex;
            if (i < 0 || i >= customerList.Count) { MessageBox.Show("اختر زبوناً أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var c = customerList[i];
            string ph = (c.Phone ?? "").Trim(); string nm = (c.Name ?? "").Trim();
            List<Ticket> match;
            try { match = ticketList.Where(t => (ph != "" && (t.Phone ?? "").Trim() == ph) || (nm != "" && (t.Customer ?? "").Trim() == nm)).ToList(); }
            catch { match = new List<Ticket>(); }
            if (match.Count == 0) { MessageBox.Show("لا توجد تذاكر لهذا الزبون.", c.Name, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var sb = new StringBuilder();
            foreach (var t in match)
                sb.AppendLine("#" + (t.Id ?? "") + "  |  " + (t.Device ?? "") + "  |  [" + (t.Status ?? "") + "]  |  " + (t.Created ?? ""));
            MessageBox.Show(sb.ToString(), "تذاكر: " + c.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
