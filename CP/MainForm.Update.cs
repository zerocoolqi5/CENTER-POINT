// -*- coding: utf-8 -*-
// Center Point - فحص التحديثات التلقائي عبر GitHub Releases — partial class
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CenterPoint
{
    public partial class MainForm
    {
        // عدّلها لو اسم المستودع مختلف
        const string GhOwner = "Zerocooliq5";
        const string GhRepo = "center-point";

        static string CurrentVersion()
        {
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v != null ? v.Major + "." + v.Minor + "." + v.Build : "0.0.0";
            }
            catch { return "0.0.0"; }
        }

        // فحص عند بدء التشغيل (صامت: لا يزعج لو ما فيه تحديث أو لا إنترنت)
        async void CheckUpdateStartup()
        {
            try { await Task.Delay(3000); } catch { }
            CheckUpdate(true);
        }

        // silent=true: يعرض فقط عند توفّر تحديث. false: يعرض النتيجة دائماً (زر الإعدادات).
        async void CheckUpdate(bool silent)
        {
            string cur = CurrentVersion();
            try
            {
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(15);
                    http.DefaultRequestHeaders.Add("User-Agent", "CenterPoint-Updater");
                    http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                    string api = "https://api.github.com/repos/" + GhOwner + "/" + GhRepo + "/releases/latest";
                    string json = await http.GetStringAsync(api);

                    string tag = "", htmlUrl = "";
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("tag_name", out var t)) tag = t.GetString() ?? "";
                        if (root.TryGetProperty("html_url", out var h)) htmlUrl = h.GetString() ?? "";
                    }
                    string latest = (tag ?? "").TrimStart('v', 'V').Trim();
                    if (latest == "")
                    {
                        if (!silent) MessageBox.Show("لم يُعثر على إصدارات منشورة بعد.", "التحديثات", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    if (IsNewer(latest, cur))
                    {
                        CpLog("INFO", "تحديث متوفّر: " + latest + " (الحالي " + cur + ")");
                        if (MessageBox.Show("يتوفّر إصدار جديد: " + latest + "\r\nالإصدار الحالي: " + cur + "\r\n\r\nفتح صفحة التحميل الآن؟",
                            "تحديث متوفّر", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            string open = htmlUrl != "" ? htmlUrl : "https://github.com/" + GhOwner + "/" + GhRepo + "/releases/latest";
                            try { Process.Start(new ProcessStartInfo { FileName = open, UseShellExecute = true }); } catch { }
                        }
                    }
                    else if (!silent)
                    {
                        MessageBox.Show("أنت على أحدث إصدار ✔  (" + cur + ")", "التحديثات", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                CpLog("WARN", "فحص التحديثات: " + ex.Message);
                if (!silent)
                    MessageBox.Show("تعذّر فحص التحديثات.\r\nتأكد من الإنترنت ومن وجود المستودع:\r\n" + GhOwner + "/" + GhRepo + "\r\n\r\n" + ex.Message,
                        "التحديثات", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // مقارنة إصدارين "a.b.c" — صحيح لو latest أحدث من cur
        static bool IsNewer(string latest, string cur)
        {
            int[] a = VerParts(latest), b = VerParts(cur);
            int n = Math.Max(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }

        static int[] VerParts(string v)
        {
            var parts = (v ?? "").Split('.');
            var r = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                int val; string digits = "";
                foreach (char c in parts[i]) { if (char.IsDigit(c)) digits += c; else break; }
                int.TryParse(digits, out val); r[i] = val;
            }
            return r;
        }
    }
}
