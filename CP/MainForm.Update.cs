// -*- coding: utf-8 -*-
// Center Point - فحص التحديثات التلقائي عبر GitHub Releases — partial class
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CenterPoint
{
    public partial class MainForm
    {
        // عدّلها لو اسم المستودع مختلف
        const string GhOwner = "zerocoolqi5";
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

        // ===== إعداد التحديث التلقائي (مفعّل افتراضياً) =====
        bool _autoUpdateLoaded = false, _autoUpdate = true;
        string AutoUpdatePath { get { return System.IO.Path.Combine(ExportsDir, "app_settings.json"); } }

        bool AutoUpdateEnabled
        {
            get { if (!_autoUpdateLoaded) LoadAutoUpdate(); return _autoUpdate; }
            set { _autoUpdate = value; _autoUpdateLoaded = true; SaveAutoUpdate(); }
        }

        void LoadAutoUpdate()
        {
            _autoUpdateLoaded = true;
            try
            {
                if (System.IO.File.Exists(AutoUpdatePath))
                    using (var doc = JsonDocument.Parse(System.IO.File.ReadAllText(AutoUpdatePath)))
                        if (doc.RootElement.TryGetProperty("autoUpdate", out var v)) _autoUpdate = v.GetBoolean();
            }
            catch { }
        }

        void SaveAutoUpdate()
        {
            try { System.IO.File.WriteAllText(AutoUpdatePath, "{\"autoUpdate\":" + (_autoUpdate ? "true" : "false") + "}", new System.Text.UTF8Encoding(false)); }
            catch { }
        }

        // فحص عند بدء التشغيل (صامت) — مُفعّل افتراضياً، ويؤجَّل أثناء أي عملية جارية
        async void CheckUpdateStartup()
        {
            if (!AutoUpdateEnabled) return;
            try { await Task.Delay(3000); } catch { }
            // تأجيل الفحص إذا فيه عملية نقل/صيانة جارية — لا نقاطعها
            int waited = 0;
            while (opBusy && waited < 1800) { try { await Task.Delay(5000); } catch { } waited += 5; }
            if (opBusy) return; // عملية طويلة — لا نزعج؛ يبقى الفحص اليدوي متاحاً
            CheckUpdate(true);
        }

        // silent=true: يعرض فقط عند توفّر تحديث. false: يعرض النتيجة دائماً (زر الإعدادات).
        async void CheckUpdate(bool silent)
        {
            // لا نبدأ فحص/تثبيت تحديث أثناء عملية نقل أو صيانة جارية
            if (opBusy)
            {
                if (!silent)
                    MessageBox.Show("هناك عملية جارية (نقل/صيانة). سيتم فحص التحديثات بعد انتهائها حفاظاً على العملية.",
                        "التحديثات", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string cur = CurrentVersion();
            try
            {
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(15);
                    http.DefaultRequestHeaders.Add("User-Agent", "CenterPoint-Updater");
                    http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                    string api = "https://api.github.com/repos/" + GhOwner + "/" + GhRepo + "/releases/latest";

                    var resp = await http.GetAsync(api);
                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // 404 = المستودع غير موجود بعد أو بلا إصدار منشور — ليست مشكلة إنترنت
                        CpLog("INFO", "لا توجد إصدارات منشورة بعد (" + GhOwner + "/" + GhRepo + ")");
                        if (!silent)
                            MessageBox.Show("لا توجد إصدارات منشورة بعد.\r\nبعد رفع المشروع على GitHub ونشر إصدار (tag) سيعمل الفحص تلقائياً.",
                                "التحديثات", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    resp.EnsureSuccessStatusCode();
                    string json = await resp.Content.ReadAsStringAsync();

                    string tag = "", htmlUrl = "", exeUrl = "";
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("tag_name", out var t)) tag = t.GetString() ?? "";
                        if (root.TryGetProperty("html_url", out var h)) htmlUrl = h.GetString() ?? "";
                        // ابحث عن ملف exe ضمن مرفقات الإصدار للتحديث التلقائي
                        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                            foreach (var a in assets.EnumerateArray())
                            {
                                string nm = a.TryGetProperty("name", out var an) ? (an.GetString() ?? "") : "";
                                if (nm.ToLowerInvariant().EndsWith(".exe") && a.TryGetProperty("browser_download_url", out var du))
                                { exeUrl = du.GetString() ?? ""; break; }
                            }
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
                        if (exeUrl != "")
                        {
                            var ch = MessageBox.Show("يتوفّر إصدار جديد: " + latest + "\r\nالإصدار الحالي: " + cur +
                                "\r\n\r\nنعم = تحديث تلقائي كامل (يُغلق البرنامج، يُحدّث، ويُعاد فتحه)\r\nلا = فتح صفحة التحميل يدوياً",
                                "تحديث متوفّر", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                            if (ch == DialogResult.Yes) { await DownloadAndInstall(exeUrl, latest, htmlUrl); return; }
                            if (ch == DialogResult.No) OpenUrl(htmlUrl != "" ? htmlUrl : "https://github.com/" + GhOwner + "/" + GhRepo + "/releases/latest");
                        }
                        else
                        {
                            // لا يوجد مرفق exe — افتح الصفحة
                            if (MessageBox.Show("يتوفّر إصدار جديد: " + latest + "\r\nالإصدار الحالي: " + cur + "\r\n\r\nفتح صفحة التحميل الآن؟ (لا يوجد ملف تثبيت مباشر في الإصدار)",
                                "تحديث متوفّر", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                                OpenUrl(htmlUrl != "" ? htmlUrl : "https://github.com/" + GhOwner + "/" + GhRepo + "/releases/latest");
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

        // OpenUrl مُعرّفة مرّة واحدة في Program.cs (static) وتُستخدم من هنا.

        // ===== تحديث تلقائي كامل: تنزيل الـexe الجديد واستبداله عبر سكربت ثم إعادة التشغيل =====
        async System.Threading.Tasks.Task DownloadAndInstall(string exeUrl, string version, string htmlUrl)
        {
            string exePath;
            try { exePath = Process.GetCurrentProcess().MainModule.FileName; }
            catch { exePath = Application.ExecutablePath; }

            // يعمل فقط على ملف exe (النشر single-file). في وضع التطوير نفتح الصفحة بدل ذلك.
            if (string.IsNullOrEmpty(exePath) || !exePath.ToLowerInvariant().EndsWith(".exe"))
            {
                OpenUrl(htmlUrl != "" ? htmlUrl : "https://github.com/" + GhOwner + "/" + GhRepo + "/releases/latest");
                return;
            }

            string tmpNew = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CenterPoint-Transfer-" + version + ".exe");
            try
            {
                try { baseTitle = "CENTER POINT — جاري تنزيل التحديث..."; Text = baseTitle; } catch { }
                CpLog("INFO", "بدء تنزيل تحديث: " + version);
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromMinutes(15);
                    http.DefaultRequestHeaders.Add("User-Agent", "CenterPoint-Updater");
                    var bytes = await http.GetByteArrayAsync(exeUrl);
                    System.IO.File.WriteAllBytes(tmpNew, bytes);
                }
                if (new System.IO.FileInfo(tmpNew).Length < 1024 * 100)
                    throw new Exception("ملف التحديث صغير/تالف.");

                // سكربت يستبدل الملف بعد إغلاق البرنامج ثم يعيد التشغيل
                int pid = Process.GetCurrentProcess().Id;
                string bat = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cp_update.bat");
                var sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("chcp 65001 >nul");
                sb.AppendLine(":wait");
                sb.AppendLine("tasklist /fi \"PID eq " + pid + "\" | find \"" + pid + "\" >nul && (ping -n 2 127.0.0.1 >nul & goto wait)");
                sb.AppendLine("ping -n 2 127.0.0.1 >nul");
                sb.AppendLine("copy /y \"" + tmpNew + "\" \"" + exePath + "\" >nul");
                sb.AppendLine("start \"\" \"" + exePath + "\"");
                sb.AppendLine("del \"%~f0\"");
                System.IO.File.WriteAllText(bat, sb.ToString(), new System.Text.UTF8Encoding(false));

                CpLog("INFO", "تثبيت التحديث " + version + " وإعادة التشغيل");
                Process.Start(new ProcessStartInfo { FileName = bat, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                try { Application.Exit(); } catch { }
                try { Environment.Exit(0); } catch { }
            }
            catch (Exception ex)
            {
                try { baseTitle = "CENTER POINT TRANSFER V12.0.0 PRO - سنتر بوينت"; Text = baseTitle; } catch { }
                ErrBox("تعذّر التحديث التلقائي: " + ex.Message + "\r\nيمكنك التحميل يدوياً من صفحة الإصدار.");
                OpenUrl(htmlUrl != "" ? htmlUrl : "https://github.com/" + GhOwner + "/" + GhRepo + "/releases/latest");
            }
        }
    }
}
