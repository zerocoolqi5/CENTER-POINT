// -*- coding: utf-8 -*-
// Center Point - نظام تذاكر الصيانة (Repair Tickets) — partial class + تخزين JSON
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using System.Text.Json;

namespace CenterPoint
{
    // نموذج تذكرة صيانة
    public class Ticket
    {
        public string Id { get; set; }
        public string Customer { get; set; }
        public string Phone { get; set; }
        public string Device { get; set; }
        public string Issue { get; set; }
        public string Status { get; set; }
        public string Created { get; set; }
        public string Updated { get; set; }
    }

    public partial class MainForm
    {
        Panel pageTickets; Button biTickets;
        TextBox tkCustomer, tkPhone, tkDevice, tkIssue; ComboBox tkStatus; ListBox tkList;
        List<Ticket> ticketList = new List<Ticket>(); string tkEditId = "";

        string TicketsPath { get => Path.Combine(DataDir, "tickets.json"); }

        void BuildTicketsPage()
        {
            pageTickets = new Panel { Location = new Point(0, 0), Size = new Size(715, 660), BackColor = BG };
            Controls.Add(pageTickets);

            var title = new Label { Text = "🎫 تذاكر الصيانة", ForeColor = Color.FromArgb(77, 166, 255), BackColor = BG, Location = new Point(20, 12), Size = new Size(675, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 15, FontStyle.Bold) };
            pageTickets.Controls.Add(title);
            Tr(title, "🎫 تذاكر الصيانة", "🎫 Repair Tickets");

            // الحقول
            TkLbl(pageTickets, "الزبون:", 20, 54);
            tkCustomer = TkBox(pageTickets, 85, 50, 250);
            TkLbl(pageTickets, "الهاتف:", 345, 54);
            tkPhone = TkBox(pageTickets, 405, 50, 290);
            TkLbl(pageTickets, "الجهاز:", 20, 90);
            tkDevice = TkBox(pageTickets, 85, 86, 250);
            TkLbl(pageTickets, "الحالة:", 345, 90);
            tkStatus = new ComboBox { Location = new Point(405, 86), Size = new Size(290, 28), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, Font = new Font("Arial", 10), FlatStyle = FlatStyle.Flat, RightToLeft = RightToLeft.Yes };
            tkStatus.Items.AddRange(new object[] { "جديد", "قيد الإصلاح", "بانتظار قطعة", "جاهز للتسليم", "مُسلّم" });
            tkStatus.SelectedIndex = 0;
            pageTickets.Controls.Add(tkStatus);
            TkLbl(pageTickets, "العطل:", 20, 126);
            tkIssue = new TextBox { Location = new Point(85, 122), Size = new Size(610, 48), Multiline = true, BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 10) };
            pageTickets.Controls.Add(tkIssue);

            // الأزرار
            var saveBtn = new Button { Text = "➕ حفظ / تحديث", Location = new Point(20, 180), Size = new Size(170, 32), BackColor = Color.FromArgb(34, 160, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            saveBtn.FlatAppearance.BorderSize = 0; saveBtn.Click += (s, e) => TkAddOrUpdate();
            var delBtn = new Button { Text = "🗑 حذف", Location = new Point(198, 180), Size = new Size(120, 32), BackColor = Color.FromArgb(150, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            delBtn.FlatAppearance.BorderSize = 0; delBtn.Click += (s, e) => TkDelete();
            var clrBtn = new Button { Text = "🧹 مسح الحقول", Location = new Point(326, 180), Size = new Size(140, 32), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            clrBtn.FlatAppearance.BorderColor = BLUE; clrBtn.Click += (s, e) => TkClear();
            var refBtn = new Button { Text = "🔄 تحديث", Location = new Point(474, 180), Size = new Size(120, 32), BackColor = BLUE, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            refBtn.FlatAppearance.BorderSize = 0; refBtn.Click += (s, e) => { TicketsLoad(); };
            pageTickets.Controls.Add(saveBtn); pageTickets.Controls.Add(delBtn); pageTickets.Controls.Add(clrBtn); pageTickets.Controls.Add(refBtn);

            // صف الربط: زبون موجود / إنشاء فاتورة
            var pickCustBtn = new Button { Text = "👥 زبون موجود", Location = new Point(20, 216), Size = new Size(210, 30), BackColor = CARD, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            pickCustBtn.FlatAppearance.BorderColor = BLUE; pickCustBtn.Click += (s, e) => TkPickCustomer();
            var mkInvBtn = new Button { Text = "🧾 إنشاء فاتورة من التذكرة", Location = new Point(238, 216), Size = new Size(260, 30), BackColor = Color.FromArgb(35, 115, 75), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9, FontStyle.Bold) };
            mkInvBtn.FlatAppearance.BorderSize = 0; mkInvBtn.Click += (s, e) => TkToInvoice();
            pageTickets.Controls.Add(pickCustBtn); pageTickets.Controls.Add(mkInvBtn);

            tkList = new ListBox { Location = new Point(20, 256), Size = new Size(675, 382), BackColor = Color.FromArgb(10, 16, 32), ForeColor = Color.FromArgb(200, 220, 255), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9), RightToLeft = RightToLeft.Yes };
            tkList.SelectedIndexChanged += (s, e) => TkSelect();
            pageTickets.Controls.Add(tkList);

            TicketsLoad();
        }

        void TkLbl(Panel p, string text, int x, int y)
        {
            p.Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(62, 20), ForeColor = Color.FromArgb(188, 214, 255), BackColor = BG, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Arial", 9, FontStyle.Bold) });
        }

        TextBox TkBox(Panel p, int x, int y, int w)
        {
            var t = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), BackColor = Color.FromArgb(16, 24, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RightToLeft = RightToLeft.Yes, Font = new Font("Arial", 10) };
            p.Controls.Add(t); return t;
        }

        void TicketsLoad()
        {
            try
            {
                if (File.Exists(TicketsPath))
                {
                    var json = File.ReadAllText(TicketsPath);
                    ticketList = JsonSerializer.Deserialize<List<Ticket>>(json) ?? new List<Ticket>();
                }
                else ticketList = new List<Ticket>();
            }
            catch { ticketList = new List<Ticket>(); }
            TicketsRefreshList();
        }

        void TicketsSave()
        {
            try
            {
                var opt = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(TicketsPath, JsonSerializer.Serialize(ticketList, opt), new UTF8Encoding(true));
            }
            catch (Exception ex) { ErrBox("تعذّر حفظ التذاكر: " + ex.Message); }
        }

        void TicketsRefreshList()
        {
            if (tkList == null) return;
            tkList.Items.Clear();
            foreach (var t in ticketList)
                tkList.Items.Add("#" + (t.Id ?? "") + "  |  " + (t.Customer ?? "") + "  |  " + (t.Device ?? "") + "  |  [" + (t.Status ?? "") + "]  |  " + (t.Created ?? ""));
        }

        int OpenTicketsCount()
        {
            try { return ticketList.Count(t => t.Status != "مُسلّم"); } catch { return 0; }
        }

        void TkAddOrUpdate()
        {
            string cust = (tkCustomer.Text ?? "").Trim();
            if (cust == "") { MessageBox.Show("اكتب اسم الزبون على الأقل.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            if (tkEditId != "")
            {
                var t = ticketList.FirstOrDefault(x => x.Id == tkEditId);
                if (t != null)
                {
                    t.Customer = cust; t.Phone = tkPhone.Text.Trim(); t.Device = tkDevice.Text.Trim();
                    t.Issue = tkIssue.Text.Trim(); t.Status = tkStatus.Text; t.Updated = now;
                }
            }
            else
            {
                var t = new Ticket
                {
                    Id = DateTime.Now.ToString("yyMMddHHmmss"),
                    Customer = cust, Phone = tkPhone.Text.Trim(), Device = tkDevice.Text.Trim(),
                    Issue = tkIssue.Text.Trim(), Status = tkStatus.Text, Created = now, Updated = now
                };
                ticketList.Insert(0, t);
            }
            TicketsSave(); TicketsRefreshList(); TkClear();
            LogOp("تذكرة صيانة: " + cust);
        }

        void TkDelete()
        {
            int i = tkList.SelectedIndex;
            if (i < 0 || i >= ticketList.Count) { MessageBox.Show("اختر تذكرة من القائمة أولاً.", "CENTER POINT", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (MessageBox.Show("حذف التذكرة: " + ticketList[i].Customer + "؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            ticketList.RemoveAt(i);
            TicketsSave(); TicketsRefreshList(); TkClear();
        }

        void TkClear()
        {
            tkEditId = "";
            if (tkCustomer != null) tkCustomer.Text = "";
            if (tkPhone != null) tkPhone.Text = "";
            if (tkDevice != null) tkDevice.Text = "";
            if (tkIssue != null) tkIssue.Text = "";
            if (tkStatus != null) tkStatus.SelectedIndex = 0;
            if (tkList != null) tkList.ClearSelected();
        }

        void TkSelect()
        {
            int i = tkList.SelectedIndex;
            if (i < 0 || i >= ticketList.Count) return;
            var t = ticketList[i];
            tkEditId = t.Id;
            tkCustomer.Text = t.Customer ?? ""; tkPhone.Text = t.Phone ?? ""; tkDevice.Text = t.Device ?? "";
            tkIssue.Text = t.Issue ?? "";
            int si = tkStatus.FindStringExact(t.Status ?? "جديد"); tkStatus.SelectedIndex = si < 0 ? 0 : si;
        }
    }
}
