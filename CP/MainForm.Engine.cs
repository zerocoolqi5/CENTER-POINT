// -*- coding: utf-8 -*-
// Center Point - محرك العمليات الموحّد (ADB / MTP / التحقق / التقارير) — partial class
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
        // ======================= المحرّك الموحّد: أدوات MTP للنقل =======================
        // تسلسل عمليات MTP (Shell32 غير آمن للتوازي على نفس الجهاز)
        static readonly object MtpGate = new object();

        // تشغيل عمل COM على خيط STA والانتظار حتى ينتهي (Shell32 يتطلب STA)
        void StaInvoke(Action work)
        {
            Exception err = null;
            var th = new System.Threading.Thread(() => { try { work(); } catch (Exception ex) { err = ex; } });
            th.SetApartmentState(System.Threading.ApartmentState.STA);
            th.IsBackground = true;
            th.Start();
            th.Join();
            if (err != null) throw err;
        }

        static string SafeName(string s)
        {
            if (s == null) return "_";
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        static string EngName(string eng)
        {
            if (eng == "adb") return "ADB (كامل/سريع)";
            if (eng == "mtp") return "MTP (وسائط)";
            return "Apple (AFC)";
        }

        // ما يدعمه كل وضع اتصال (صور/فيديو/صوت/مستندات/جهات اتصال)
        static string CapsLine(string eng)
        {
            if (eng == "adb") return "صور ✔  فيديو ✔  صوت ✔  مستندات ✔  جهات اتصال ✔";
            if (eng == "mtp") return "صور ✔  فيديو ✔  صوت ✔  مستندات ✔  جهات اتصال ✘ (تحتاج ADB)";
            return "صور ✔  فيديو ✔  مستندات ✔  جهات اتصال (VCF يدوي)";
        }

        // إيجاد عنصر جهاز MTP بالاسم ضمن «هذا الكمبيوتر»
        dynamic MtpDeviceItemByName(dynamic sh, string name)
        {
            dynamic pc = sh.NameSpace(17);
            foreach (dynamic it in pc.Items())
            {
                bool isF = false; string path = "", nm = "";
                try { isF = it.IsFolder; path = (string)it.Path; nm = (string)it.Name; } catch { }
                if (isF && path != null && path.StartsWith("::") && nm == name) return it;
            }
            return null;
        }

        // جذر المخزن الأول داخل جهاز MTP (التخزين الداخلي عادةً)
        dynamic MtpStorageRoot(dynamic deviceFolder)
        {
            foreach (dynamic st in deviceFolder.Items())
            { try { if (st.IsFolder) return st.GetFolder; } catch { } }
            return null;
        }

        // التنقّل إلى مسار أندرويد (/sdcard/DCIM ...) داخل MTP، مع إنشائه عند الحاجة
        dynamic MtpNavigate(dynamic deviceFolder, string androidPath, bool create)
        {
            dynamic cur = MtpStorageRoot(deviceFolder);
            if (cur == null) return null;
            string rel = (androidPath ?? "").Replace("/sdcard/", "").Replace("/sdcard", "").Trim('/');
            if (rel == "") return cur;
            foreach (var seg in rel.Split('/'))
            {
                if (string.IsNullOrEmpty(seg)) continue;
                dynamic next = null;
                foreach (dynamic it in cur.Items())
                { try { if (it.IsFolder && (string)it.Name == seg) { next = it.GetFolder; break; } } catch { } }
                if (next == null)
                {
                    if (!create) return null;
                    try { cur.NewFolder(seg); } catch { }
                    foreach (dynamic it in cur.Items())
                    { try { if (it.IsFolder && (string)it.Name == seg) { next = it.GetFolder; break; } } catch { } }
                    if (next == null) return null;
                }
                cur = next;
            }
            return cur;
        }

        // قراءة مجلد من جهاز MTP إلى مجلد محلي (يحافظ على البنية)
        (bool ok, string outp) ReadFolderMtp(Device dev, string androidPath, string local)
        {
            bool ok = false; string msg = "";
            try
            {
                StaInvoke(() =>
                {
                    lock (MtpGate)
                    {
                        dynamic sh = ShellApp();
                        dynamic devItem = MtpDeviceItemByName(sh, dev.Model);
                        if (devItem == null) { msg = "جهاز MTP غير موجود — تأكد من وضع «نقل الملفات»."; ok = false; return; }
                        // نفرّق بين «تخزين غير متاح» (فشل) و«المجلد غير موجود» (فارغ شرعي)
                        dynamic root = MtpStorageRoot(devItem.GetFolder);
                        if (root == null)
                        {
                            msg = "تعذّر الوصول إلى تخزين الجهاز عبر MTP (قد يكون الهاتف مقفلاً أو بوضع «شحن فقط»). افتح قفل الهاتف واختر «نقل الملفات» ثم أعد المحاولة.";
                            ok = false; return;   // ليست نتيجة فارغة ناجحة — بل فشل وصول
                        }
                        dynamic src = MtpNavigate(devItem.GetFolder, androidPath, false);
                        if (src == null) { ok = true; msg = "المجلد غير موجود على الجهاز (لا توجد ملفات من هذا النوع)"; return; } // المجلد فعلاً غير موجود = فارغ شرعي
                        Directory.CreateDirectory(local);
                        MtpCopyFolderOut(sh, src, local);
                        ok = true;
                    }
                });
            }
            catch (Exception ex) { return (false, ex.Message); }
            return (ok, msg);
        }

        // نسخ محتوى مجلد MTP (متضمّناً المجلدات الفرعية) إلى مجلد محلي
        void MtpCopyFolderOut(dynamic sh, dynamic folder, string localDir)
        {
            Directory.CreateDirectory(localDir);
            dynamic ns = sh.NameSpace(localDir);
            foreach (dynamic it in folder.Items())
            {
                if (xferCancel) return;
                bool isF = false; string nm = "";
                try { isF = it.IsFolder; nm = (string)it.Name; } catch { continue; }
                if (isF)
                {
                    dynamic sub = null; try { sub = it.GetFolder; } catch { }
                    if (sub != null) MtpCopyFolderOut(sh, sub, Path.Combine(localDir, SafeName(nm)));
                }
                else
                {
                    try
                    {
                        ns.CopyHere(it, 0x214);
                        string copied = MtpWaitFile(localDir, nm, 300);
                        if (copied == null)
                            throw new IOException("MTP copy did not reach a stable file size: " + nm);
                    }
                    catch (Exception ex)
                    {
                        LogError("MTP read", nm + " | " + ex.Message);
                        throw;
                    }
                }
            }
        }

        // كتابة قسم كامل إلى جهاز MTP (نسخة + تحقق بالحجم). ترجع true إذا أُلغيت العملية.
        bool MtpWriteCategory(Device dev, string androidPath, string localRoot, string[] files,
                              Action<string, bool, long> onFile, Func<bool> cancel)
        {
            bool cancelled = false;
            try
            {
                StaInvoke(() =>
                {
                    lock (MtpGate)
                    {
                        dynamic sh = ShellApp();
                        dynamic devItem = MtpDeviceItemByName(sh, dev.Model);
                        if (devItem == null)
                        {
                            foreach (var f in files) { long z = 0; try { z = new FileInfo(f).Length; } catch { } onFile(Path.GetFileName(f), false, z); }
                            return;
                        }
                        dynamic deviceFolder = devItem.GetFolder;
                        var folderCache = new Dictionary<string, object>();
                        foreach (var f in files)
                        {
                            if (cancel()) { cancelled = true; return; }
                            string name = Path.GetFileName(f);
                            long sz = 0; try { sz = new FileInfo(f).Length; } catch { }
                            string rel = f.Substring(localRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
                            int slash = rel.LastIndexOf('/');
                            string relDir = slash > 0 ? rel.Substring(0, slash) : "";
                            string destPath = relDir == "" ? androidPath : androidPath + "/" + relDir;
                            dynamic destFolder;
                            if (folderCache.ContainsKey(destPath)) destFolder = folderCache[destPath];
                            else { destFolder = MtpNavigate(deviceFolder, destPath, true); folderCache[destPath] = (object)destFolder; }
                            if (destFolder == null) { onFile(name, false, sz); continue; }
                            try
                            {
                                bool nameExists = false;
                                foreach (dynamic existing in destFolder.Items())
                                {
                                    try { if ((string)existing.Name == name) { nameExists = true; break; } }
                                    catch { }
                                }
                                if (nameExists)
                                {
                                    LogError("MTP write", name + " | destination name already exists; skipped to prevent false success/overwrite.");
                                    onFile(name, false, sz);
                                    continue;
                                }
                                dynamic localNs = sh.NameSpace(Path.GetDirectoryName(f));
                                dynamic localItem = localNs.ParseName(name);
                                if (localItem == null) { onFile(name, false, sz); continue; }
                                destFolder.CopyHere(localItem, 0x214);
                                bool okv = MtpDestHas(destFolder, name, sz, 180);
                                onFile(name, okv, sz);
                            }
                            catch { onFile(name, false, sz); }
                        }
                    }
                });
            }
            catch { }
            return cancelled;
        }

        // ============ مشترك ============
        void Log(string m) { if (log.InvokeRequired) { log.BeginInvoke(new Action(() => Log(m))); return; } log.AppendText(m + "\r\n"); }
        void SetProgress(double p) { if (bar.InvokeRequired) { bar.BeginInvoke(new Action(() => SetProgress(p))); return; } int v = (int)Math.Max(0, Math.Min(100, p)); bar.Value = v; lblPct.Text = v + "%"; }

        volatile bool _refreshing;

        // تحديث غير متزامن: الكشف (adb/MTP) على خيط خلفي كي لا تتجمّد الواجهة
        void Refresh2(Action after = null)
        {
            if (cbSrc != null && cbSrc.InvokeRequired) { cbSrc.BeginInvoke(new Action(() => Refresh2(after))); return; }
            if (_refreshing) { if (after != null) after(); return; }
            _refreshing = true;
            try { Log("جاري تحديث الأجهزة..."); } catch { }
            System.Threading.Tasks.Task.Run(() =>
            {
                List<Device> devs;
                try { devs = Devices.All(); } catch { devs = new List<Device>(); }
                try
                {
                    if (IsHandleCreated) BeginInvoke(new Action(() => { ApplyDevices(devs); _refreshing = false; if (after != null) after(); }));
                    else { _refreshing = false; }
                }
                catch { _refreshing = false; }
            });
        }

        // تعبئة القوائم — تعمل على خيط الواجهة فقط
        void ApplyDevices(List<Device> devs)
        {
            devices = devs ?? new List<Device>();
            var labels = devices.Select(d => (object)d.ToString()).ToArray();
            cbSrc.Items.Clear(); cbDst.Items.Clear(); cbDev.Items.Clear();
            galSrc.Items.Clear(); galDst.Items.Clear(); bkDev.Items.Clear();
            appSrc.Items.Clear(); appDst.Items.Clear(); accDev.Items.Clear(); fmDev.Items.Clear();
            cbSrc.Items.AddRange(labels); cbDst.Items.AddRange(labels); cbDev.Items.AddRange(labels);
            galSrc.Items.AddRange(labels); galDst.Items.AddRange(labels); bkDev.Items.AddRange(labels);
            appSrc.Items.AddRange(labels); appDst.Items.AddRange(labels); accDev.Items.AddRange(labels); fmDev.Items.AddRange(labels);
            if (devices.Count >= 1) { cbSrc.SelectedIndex = 0; cbDev.SelectedIndex = 0; galSrc.SelectedIndex = 0; bkDev.SelectedIndex = 0; appSrc.SelectedIndex = 0; accDev.SelectedIndex = 0; fmDev.SelectedIndex = 0; }
            if (devices.Count >= 2) { cbDst.SelectedIndex = 1; galDst.SelectedIndex = 1; appDst.SelectedIndex = 1; }
            int na = devices.Count(d => d.Kind == "android"), ni = devices.Count(d => d.Kind == "ios"), nm = devices.Count(d => d.Kind == "mtp");
            Log("الأجهزة المتصلة: " + na + " أندرويد (ADB)، " + ni + " آيفون، " + nm + " MTP.");
            if (nm > 0) Log("📟 جهاز MTP = أندرويد بوضع «نقل الملفات» (وسائط فقط). 📱 ADB = نقل كامل وسريع.");
            if (Devices.Unauthorized > 0) Log("⚠ " + Devices.Unauthorized + " جهاز غير مصرّح — افتح الموبايل واضغط «السماح بتصحيح USB».");
            if (Devices.Offline > 0) Log("⚠ " + Devices.Offline + " جهاز غير متصل (offline) — افصل الكيبل وأعد توصيله.");
            if (devices.Count < 2) Log("⚠ للنقل تحتاج جهازين. فعّل USB Debugging/الوثوق.");
            RefreshHome();
        }

        // تحذير صريح: الكتابة على الآيفون تجريبية وقد لا تعمل بالكامل (قيود آبل)
        bool ConfirmIosWrite()
        {
            return MessageBox.Show(
                "⚠ إدخال الصور/الملفات إلى الآيفون ميزة تجريبية وغير مضمونة بالكامل بسبب قيود آبل، وقد لا تظهر في تطبيق الصور.\n\n" +
                "للنتيجة المضمونة استخدم iCloud أو AirDrop.\n\nهل تريد المتابعة على أي حال؟",
                "تنبيه - وجهة آيفون", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        void Start()
        {
            if (cbSrc.SelectedIndex < 0 || cbDst.SelectedIndex < 0) { MessageBox.Show("اختر المصدر والوجهة."); return; }
            if (cbSrc.SelectedIndex == cbDst.SelectedIndex) { MessageBox.Show("لازم جهازين مختلفين."); return; }
            var sel = transferSel.ToList();
            if (sel.Count == 0) { MessageBox.Show("اختر نوع بيانات واحد على الأقل."); return; }
            var s = devices[cbSrc.SelectedIndex]; var d = devices[cbDst.SelectedIndex];
            if (d.Kind == "ios" && !ConfirmIosWrite()) return;
            if (!BeginOp("نقل البيانات")) return;
            btnStart.Enabled = false; btnStart.Text = "جاري النقل...";
            SetProgress(0);
            Task.Run(() => Transfer(s, d, sel));
        }

        void HandleContacts(Device s, Device d)
        {
            Log("── جهات الاتصال / الأسماء ──");
            if (s.Kind == "mtp") { Log("  جهات الاتصال تحتاج ADB (فعّل «تصحيح USB»). غير متاحة عبر MTP."); return; }
            if (s.Kind != "android") { Log("  قراءة الأسماء من الآيفون غير مدعومة. شاركها كملف vcf عبر AirDrop."); return; }
            string exports = ExportsDir;
            string vcf = Path.Combine(exports, "CenterPoint_Contacts.vcf");
            Log("  قراءة الأسماء من المصدر...");
            var r = Shell.Run("adb", "-s " + s.Id + " shell content query --uri content://com.android.contacts/data --where " + Shell.Q("mimetype='vnd.android.cursor.item/phone_v2'") + " --projection display_name:data1");
            if (!r.ok) { Log("  فشل: " + Trim(r.outp)); return; }
            var people = new Dictionary<string, List<string>>();
            foreach (var line in r.outp.Replace("\r", "").Split('\n'))
            {
                var m = Regex.Match(line, @"display_name=(.*?),\s*data1=(.*?)\s*$");
                if (!m.Success) continue;
                string name = m.Groups[1].Value.Trim(), num = m.Groups[2].Value.Trim();
                if (name == "" || num == "" || name == "null") continue;
                if (!people.ContainsKey(name)) people[name] = new List<string>();
                if (!people[name].Contains(num)) people[name].Add(num);
            }
            if (people.Count == 0) { Log("  لم يتم العثور على أسماء."); return; }
            var sb = new StringBuilder();
            foreach (var kv in people)
            {
                sb.AppendLine("BEGIN:VCARD"); sb.AppendLine("VERSION:3.0"); sb.AppendLine("FN:" + kv.Key);
                foreach (var n in kv.Value) sb.AppendLine("TEL;TYPE=CELL:" + n);
                sb.AppendLine("END:VCARD");
            }
            File.WriteAllText(vcf, sb.ToString(), new UTF8Encoding(false));
            Log("  تم تصدير " + people.Count + " اسم ← نسخة في مجلد exports");
            if (d.Kind == "android")
            {
                string remote = "/sdcard/Download/CenterPoint_Contacts.vcf";
                var p1 = Shell.Run("adb", "-s " + d.Id + " push " + Shell.Q(vcf) + " " + remote);
                if (!p1.ok) { Log("  فشل الإرسال: " + Trim(p1.outp)); return; }
                Shell.Run("adb", "-s " + d.Id + " shell am start -a android.intent.action.VIEW -d file://" + remote + " -t text/x-vcard");
                Log("  ✔ افتح شاشة جهاز الوجهة واضغط (استيراد/موافق).");
            }
            else Log("  الوجهة آيفون: أرسل ملف exports بـ AirDrop وافتحه ليستورد.");
        }

        void SaveReport(Device s, Device d, int total, int ok, int fail, List<string> failed)
        {
            try
            {
                string exports = ExportsDir;
                string stamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm");
                string path = Path.Combine(exports, "CenterPoint_Report_" + stamp + ".txt");
                var sb = new StringBuilder();
                sb.AppendLine("تقرير نقل البيانات - سنتر بوينت");
                sb.AppendLine("التاريخ: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"));
                sb.AppendLine("من: " + s.Model);
                sb.AppendLine("إلى: " + d.Model);
                sb.AppendLine("----------------------------------------");
                sb.AppendLine("الإجمالي: " + total + "  |  نجح: " + ok + "  |  فشل: " + fail);
                if (fail > 0) { sb.AppendLine("الملفات الفاشلة:"); foreach (var x in failed) sb.AppendLine("  - " + x); }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            }
            catch { }
        }

        static string FmtSize(long b)
        {
            if (b >= 1073741824L) return (b / 1073741824.0).ToString("0.0") + " GB";
            if (b >= 1048576L) return (b / 1048576.0).ToString("0.0") + " MB";
            if (b >= 1024L) return (b / 1024.0).ToString("0.0") + " KB";
            return b + " B";
        }

        string SaveReportDetailed(Device s, Device d, int total, int ok, int fail, long bytes, double secs,
                                  List<(string name, int total, int ok, int fail, long bytes)> cats, List<string> failed)
        {
            try
            {
                string exports = ExportsDir;
                string stamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
                string path = Path.Combine(exports, "CenterPoint_Report_" + stamp + ".txt");
                var sb = new StringBuilder();
                sb.AppendLine("========================================");
                sb.AppendLine("     تقرير نقل البيانات - CENTER POINT");
                sb.AppendLine("========================================");
                string sEng = s.Kind == "ios" ? "ios" : (s.Kind == "mtp" ? "mtp" : "adb");
                string dEng = d.Kind == "ios" ? "ios" : (d.Kind == "mtp" ? "mtp" : "adb");
                sb.AppendLine("التاريخ: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("من:  " + s.Model + "   (" + s.Id + ")   [" + EngName(sEng) + "]");
                sb.AppendLine("إلى: " + d.Model + "   (" + d.Id + ")   [" + EngName(dEng) + "]");
                sb.AppendLine("وضع النقل: المصدر " + EngName(sEng) + "  →  الوجهة " + EngName(dEng));
                sb.AppendLine("قدرات الوجهة: " + CapsLine(dEng));
                sb.AppendLine("----------------------------------------");
                sb.AppendLine("تفصيل الأقسام:");
                foreach (var c in cats)
                    sb.AppendLine(string.Format("   - {0}: {1}/{2} ملف   ({3}){4}",
                        c.name, c.ok, c.total, FmtSize(c.bytes), c.fail > 0 ? "   فشل " + c.fail : ""));
                sb.AppendLine("----------------------------------------");
                sb.AppendLine("الإجمالي:     " + total + " ملف");
                sb.AppendLine("نجح:         " + ok);
                sb.AppendLine("فشل:         " + fail);
                double okRate = total > 0 ? (ok * 100.0 / total) : 0;
                sb.AppendLine("نسبة النجاح:  " + okRate.ToString("0.0") + "%");
                sb.AppendLine("الحجم الكلي:  " + FmtSize(bytes));
                sb.AppendLine("المدة:        " + (int)secs + " ثانية");
                if (secs > 0)
                {
                    sb.AppendLine("متوسط السرعة: " + FmtSize((long)(bytes / secs)) + "/ثانية  (" + (bytes / 1024.0 / 1024.0 / Math.Max(secs, 0.001)).ToString("0.0") + " ميجابايت/ث)");
                    sb.AppendLine("ملفات/ثانية:  " + (ok / Math.Max(secs, 0.001)).ToString("0.0"));
                }
                sb.AppendLine("التحقق:       تمّ فحص حجم كل ملف على الوجهة (ليس عيّنات).");
                if (fail > 0)
                {
                    sb.AppendLine("----------------------------------------");
                    sb.AppendLine("الملفات الفاشلة (" + failed.Count + "):");
                    foreach (var x in failed) sb.AppendLine("   x " + x);
                }
                // آخر الأخطاء المسجّلة
                try
                {
                    string el = Path.Combine(exports, "errors.log");
                    if (File.Exists(el))
                    {
                        var lines = File.ReadAllLines(el).Where(x => x.Trim() != "").ToList();
                        if (lines.Count > 0)
                        {
                            sb.AppendLine("----------------------------------------");
                            sb.AppendLine("آخر الأخطاء المسجّلة (" + Math.Min(10, lines.Count) + " من " + lines.Count + "):");
                            foreach (var ln in lines.Skip(Math.Max(0, lines.Count - 10))) sb.AppendLine("   ! " + ln);
                        }
                    }
                }
                catch { }
                sb.AppendLine("========================================");
                sb.AppendLine("سنتر بوينت للاتصالات");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                try { AppendTransferHistory(s, d, total, ok, fail, bytes, secs); } catch { }
                return path;
            }
            catch { return null; }
        }

        public class TransferHist
        {
            public string Date { get; set; }
            public string From { get; set; }
            public string To { get; set; }
            public int Total { get; set; }
            public int Ok { get; set; }
            public int Fail { get; set; }
            public long Bytes { get; set; }
            public double Secs { get; set; }
        }

        string TransferHistJson { get { return Path.Combine(ExportsDir, "transfer_history.json"); } }

        // سجل نقل تراكمي — إضافة فقط، لا يؤثر على محرك النقل
        void AppendTransferHistory(Device s, Device d, int total, int ok, int fail, long bytes, double secs)
        {
            List<TransferHist> hist;
            try { hist = File.Exists(TransferHistJson) ? (System.Text.Json.JsonSerializer.Deserialize<List<TransferHist>>(File.ReadAllText(TransferHistJson)) ?? new List<TransferHist>()) : new List<TransferHist>(); }
            catch { hist = new List<TransferHist>(); }
            hist.Insert(0, new TransferHist { Date = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"), From = s != null ? s.Model : "", To = d != null ? d.Model : "", Total = total, Ok = ok, Fail = fail, Bytes = bytes, Secs = secs });
            if (hist.Count > 500) hist = hist.Take(500).ToList();
            try { File.WriteAllText(TransferHistJson, System.Text.Json.JsonSerializer.Serialize(hist, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(true)); }
            catch { }
        }

        // إحصائية النقل التراكمية (مدى الحياة)
        void TransferStats()
        {
            try
            {
                List<TransferHist> hist = File.Exists(TransferHistJson) ? (System.Text.Json.JsonSerializer.Deserialize<List<TransferHist>>(File.ReadAllText(TransferHistJson)) ?? new List<TransferHist>()) : new List<TransferHist>();
                if (hist.Count == 0) { MessageBox.Show("لا يوجد سجل نقل بعد. أكمل عملية نقل وستُحتسب هنا.", "إحصائية النقل", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                int ops = hist.Count, files = hist.Sum(h => h.Ok), fails = hist.Sum(h => h.Fail);
                long bytes = hist.Sum(h => h.Bytes); double secs = hist.Sum(h => h.Secs);
                int totalFiles = hist.Sum(h => h.Total);
                double rate = totalFiles > 0 ? (files * 100.0 / totalFiles) : 0;
                var msg = "عدد عمليات النقل: " + ops + "\r\n" +
                          "إجمالي الملفات المنقولة: " + files + "\r\n" +
                          "ملفات فشلت: " + fails + "\r\n" +
                          "نسبة النجاح: " + rate.ToString("0.0") + "%\r\n" +
                          "الحجم الكلي المنقول: " + FmtSize(bytes) + "\r\n" +
                          "إجمالي الوقت: " + (int)(secs / 60) + " دقيقة\r\n" +
                          "آخر نقل: " + hist[0].Date + " — من " + hist[0].From + " إلى " + hist[0].To;
                MessageBox.Show(msg, "📊 إحصائية النقل التراكمية", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ErrBox("تعذّر قراءة إحصائية النقل: " + ex.Message); }
        }

        void Transfer(Device s, Device d, List<string> sel)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "cp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(tmp);
                var t0 = DateTime.UtcNow;
                xStart = t0; xTotal = 0; xDone = 0; xBytes = 0; xferCancel = false;
                Log(""); Log("من " + s.Model + "  ←  إلى " + d.Model + "  (" + t0.ToString("HH:mm:ss") + ")");
                // === المحرّك الموحّد: يكتشف مسار كل جهاز تلقائياً ويوجّه عبره (ADB / MTP / Apple) ===
                Log("🔀 المحرّك الموحّد — تحديد المسار تلقائياً لكل جهاز...");
                string sEng = s.Kind == "ios" ? "ios" : (s.Kind == "mtp" ? "mtp" : "adb");
                string dEng = d.Kind == "ios" ? "ios" : (d.Kind == "mtp" ? "mtp" : "adb");
                Log("   المصدر: " + EngName(sEng) + "   →   الوجهة: " + EngName(dEng));
                Log("   قدرات المصدر: " + CapsLine(sEng));
                Log("   قدرات الوجهة: " + CapsLine(dEng));
                // أجهزة ADB: تأكّد أنها ما زالت متصلة ومصرّحة
                if (sEng == "adb" || dEng == "adb")
                {
                    var adl = Shell.Run("adb", "devices", 15000);
                    bool srcOk = sEng != "adb" || Regex.IsMatch(adl.outp, Regex.Escape(s.Id) + @"\s+device");
                    bool dstOk = dEng != "adb" || Regex.IsMatch(adl.outp, Regex.Escape(d.Id) + @"\s+device");
                    // انقطاع لحظي؟ ننتظر عودة الجهاز بضع ثوانٍ قبل التوقّف
                    if (!srcOk && sEng == "adb") { Log("   ⏳ المصدر غير ظاهر — بانتظار عودته..."); srcOk = Devices.WaitForAdb(s, 8); }
                    if (!dstOk && dEng == "adb") { Log("   ⏳ الوجهة غير ظاهرة — بانتظار عودتها..."); dstOk = Devices.WaitForAdb(d, 8); }
                    if (!srcOk || !dstOk)
                    {
                        if (!srcOk) Log("   ⚠ المصدر غير متاح عبر ADB — فعّل «تصحيح USB» والسماح، أو اختر الجهاز بوضع 📟 MTP.");
                        if (!dstOk) Log("   ⚠ الوجهة غير متاحة عبر ADB — فعّل «تصحيح USB» والسماح، أو اختر الجهاز بوضع 📟 MTP.");
                        SetCurrent("⚠ جهاز ADB غير متصل");
                        LogError("المحرّك الموحّد", "ADB غير متاح: src=" + srcOk + " dst=" + dstOk);
                        PostWeb(JsonSerializer.Serialize(new { type = "xdone", status = "⚠ جهاز ADB غير متصل", done = 0, toast = "جهاز ADB غير متصل. فعّل تصحيح USB أو اختر الجهاز بوضع MTP." }));
                        return;
                    }
                    Log("   ✔ ADB: نقل كامل وسريع متاح.");
                }
                if (sEng == "mtp" || dEng == "mtp")
                    Log("   ℹ MTP: وسائط وملفات فقط (جهات الاتصال تحتاج ADB). التحقق بالحجم مُفعّل.");

                bool didContacts = false;
                if (sel.Contains(CONTACTS)) { sel.Remove(CONTACTS); HandleContacts(s, d); didContacts = true; }

                var jobs = new List<Category>();
                foreach (var name in sel)
                {
                    var c = cats.FirstOrDefault(x => x.Name == name);
                    if (c == null) continue;
                    // MTP وأندرويد يستخدمان مسار /sdcard نفسه؛ الآيفون يستخدم مسار Apple
                    string sp = s.Kind == "ios" ? c.Ios : c.Android;
                    string dp = d.Kind == "ios" ? c.Ios : c.Android;
                    if (sp == null || dp == null) { Log("── " + name + " ── غير مدعوم، تخطّي."); continue; }
                    jobs.Add(c);
                }
                if (jobs.Count == 0) { if (!didContacts) Log("لا يوجد قسم قابل للنقل."); SetProgress(100); Log(""); Log("✅ انتهى."); return; }

                // === قراءة متوازية بحدّ آمن (مهمّتان كحدّ أقصى) لتفادي إرهاق USB ===
                var roots = new List<(Category cat, string local, string dp)>();
                var rootsLock = new object();
                Log("⚙ قراءة الأقسام (حتى مهمّتين معاً)...");
                using (var sem = new System.Threading.SemaphoreSlim(2))
                {
                    var tasks = new List<Task>();
                    foreach (var c in jobs)
                    {
                        var cc = c;
                        tasks.Add(Task.Run(() =>
                        {
                            sem.Wait();
                            try
                            {
                                if (xferCancel) return;
                                string sp = s.Kind == "ios" ? cc.Ios : cc.Android;
                                string dp = d.Kind == "ios" ? cc.Ios : cc.Android;
                                if (s.Kind == "android")
                                {
                                    var chk = Shell.Run("adb", "-s " + s.Id + " shell ls " + sp);
                                    if (!chk.ok || chk.outp.Contains("No such file")) { Log("── " + cc.Name + " ── فارغ، تخطّي."); return; }
                                }
                                Log("── " + cc.Name + " ── جاري القراءة من المصدر...");
                                string local = Path.Combine(tmp, Regex.Replace(cc.Name, @"[^\w]", "_"));
                                var rd = ReadFolder(s, sp, local);
                                if (!rd.ok) { Log("  فشل القراءة " + cc.Name + ": " + Trim(rd.outp)); LogError("قراءة/" + cc.Name, Trim(rd.outp)); return; }
                                lock (rootsLock) roots.Add((cc, local, dp));
                            }
                            finally { sem.Release(); }
                        }));
                    }
                    Task.WaitAll(tasks.ToArray());
                }

                var plan = new List<(Category cat, string local, string dp, string[] files)>();
                int total = 0;
                foreach (var r in roots)
                {
                    string[] files = Directory.Exists(r.local) ? Directory.GetFiles(r.local, "*", SearchOption.AllDirectories) : new string[0];
                    plan.Add((r.cat, r.local, r.dp, files)); total += files.Length;
                }
                if (total == 0) { if (!didContacts) Log("لا توجد ملفات للنقل."); SetProgress(100); Log(""); Log("✅ انتهى."); PostWeb(JsonSerializer.Serialize(new { type = "xdone", status = "لا توجد ملفات للنقل", done = 0, toast = "لا توجد ملفات مطابقة للنقل." })); return; }
                Log(""); Log("إجمالي الملفات: " + total);
                xTotal = total; PostXfer("بدء النقل…");

                // === فحص مساحة الهاتف الهدف قبل الكتابة عليه ===
                if (d.Kind == "android")
                {
                    long needBytes = 0;
                    foreach (var r in plan) foreach (var f in r.files) { try { needBytes += new FileInfo(f).Length; } catch { } }
                    var tf = Devices.Storage(d);
                    long freeBytes = (long)(tf.free * 1024.0 * 1024.0 * 1024.0);
                    if (tf.tot > 0 && needBytes > freeBytes)
                    {
                        Log("");
                        Log("⛔ مساحة الهاتف الهدف لا تكفي للنقل:");
                        Log("   المطلوب: " + FmtSize(needBytes) + "   |   المتاح: " + FmtSize(freeBytes));
                        Log("   فرّغ مساحة على الهاتف الهدف ثم أعد المحاولة.");
                        SetCurrent("⛔ مساحة الهدف غير كافية");
                        return;
                    }
                    if (tf.tot > 0) Log("✔ فحص المساحة: المطلوب " + FmtSize(needBytes) + " ضمن المتاح " + FmtSize(freeBytes) + ".");
                }

                int done = 0, okCount = 0, failCount = 0;
                long totalBytes = 0;
                var failed = new List<string>();
                var catStats = new List<(string name, int total, int ok, int fail, long bytes)>();
                var madeDirs = new HashSet<string>();   // منع تكرار إنشاء نفس المجلد (تسريع)
                bool aborted = false;                   // يصير true عند انقطاع الهاتف
                foreach (var r in plan)
                {
                    if (aborted) break;
                    if (xferCancel) { aborted = true; break; }
                    if (r.files.Length == 0) continue;
                    Log(""); Log("── " + r.cat.Name + " → الوجهة (" + r.files.Length + " ملف) ──");
                    int cok = 0, cfail = 0; long cbytes = 0;

                    // === مسار سريع (أندرويد): دفع محتوى المجلد مباشرةً للوجهة بأمر واحد ===
                    // الأمان: لا نحذف أي مجلد في الوجهة إطلاقاً. عند عدم التأكد نرجع للنقل ملف ملف فقط.
                    bool fastDone = false;
                    if (d.Kind == "android")
                    {
                        Log("  ⚡ نقل سريع (دفعة واحدة)...");
                        Shell.Run("adb", "-s " + d.Id + " shell mkdir -p " + Shell.Q(r.dp));
                        var batch = Shell.Run("adb", "-s " + d.Id + " push " + Shell.Q(r.local + Path.DirectorySeparatorChar + ".") + " " + Shell.Q(r.dp), 3600000);
                        if (IsDisconnect(batch.outp))
                        {
                            Log("  ⏳ انقطع الهاتف أثناء الدفعة — بانتظار عودته...");
                            if (Devices.WaitForAdb(d, 10)) { Log("  ✔ عاد الاتصال — إعادة المحاولة."); Shell.Run("adb", "-s " + d.Id + " shell mkdir -p " + Shell.Q(r.dp)); batch = Shell.Run("adb", "-s " + d.Id + " push " + Shell.Q(r.local + Path.DirectorySeparatorChar + ".") + " " + Shell.Q(r.dp), 3600000); if (IsDisconnect(batch.outp)) aborted = true; }
                            else aborted = true;
                        }
                        if (!aborted)
                        {
                            // === تحقق كامل: حجم كل ملف على الوجهة (ليس عيّنات) ===
                            Log("  🔎 تحقق من كل ملف بالحجم على الوجهة...");
                            var bad = AdbVerifyAll(d, r.dp, r.local, r.files);
                            // عدّ الملفات المؤكّدة فوراً
                            foreach (var f in r.files)
                            {
                                string rel = f.Substring(r.local.Length).TrimStart('\\', '/').Replace('\\', '/');
                                if (bad.Contains(rel)) continue;
                                long sz = 0; try { sz = new FileInfo(f).Length; } catch { }
                                done++; okCount++; cok++; cbytes += sz; totalBytes += sz;
                            }
                            SetProgress((double)done / total * 100); xDone = done; xBytes = totalBytes; PostXfer("⚡ " + r.cat.Name);
                            if (bad.Count == 0)
                            {
                                SetCurrent("⚡ " + r.cat.Name + " — " + r.files.Length + " ملف (تحقق كامل)");
                                Log("  ✔ " + r.files.Length + " ملف — تحقق كامل بالحجم  (" + FmtSize(cbytes) + ")");
                            }
                            else
                            {
                                // إعادة الملفات غير المؤكّدة ملف-ملف مع تحقق فردي
                                Log("  ↩ " + bad.Count + " ملف غير مؤكّد — إعادة ملف-ملف مع تحقق...");
                                foreach (var f in r.files)
                                {
                                    if (xferCancel) { aborted = true; break; }
                                    string rel = f.Substring(r.local.Length).TrimStart('\\', '/').Replace('\\', '/');
                                    if (!bad.Contains(rel)) continue;
                                    string name = Path.GetFileName(f);
                                    long sz = 0; try { sz = new FileInfo(f).Length; } catch { }
                                    done++; SetCurrent("⇄ " + name + "  (" + done + "/" + total + ")");
                                    string destRemote = r.dp + "/" + rel;
                                    int slash = destRemote.LastIndexOf('/'); string destDir = slash > 0 ? destRemote.Substring(0, slash) : r.dp;
                                    if (madeDirs.Add(destDir)) Shell.Run("adb", "-s " + d.Id + " shell mkdir -p " + Shell.Q(destDir));
                                    var pr = Shell.Run("adb", "-s " + d.Id + " push " + Shell.Q(f) + " " + Shell.Q(destRemote));
                                    bool vok = false;
                                    if (pr.ok) { var stt = Shell.Run("adb", "-s " + d.Id + " shell stat -c %s " + Shell.Q(destRemote), 30000); long rsz = 0; long.TryParse((stt.outp ?? "").Trim(), out rsz); vok = (rsz == sz && sz > 0) || (sz == 0 && stt.ok); }
                                    if (vok) { okCount++; cok++; cbytes += sz; totalBytes += sz; Log("  [" + done + "/" + total + "]  ✔ " + name + "  (" + FmtSize(sz) + ")"); }
                                    else { failCount++; cfail++; failed.Add(r.cat.Name + "  |  " + name); Log("  [" + done + "/" + total + "]  ✗ فشل/لم يتحقق: " + name); LogError("نقل/" + r.cat.Name, name + " | " + Trim(pr.outp)); }
                                    SetProgress((double)done / total * 100); xDone = done; xBytes = totalBytes; PostXfer("⇄ " + name);
                                    if (!pr.ok && IsDisconnect(pr.outp)) { aborted = true; break; }
                                }
                            }
                            fastDone = true;
                        }
                    }

                    // === مسار MTP: كتابة القسم كاملاً عبر Shell32 (نسخ + تحقق بالحجم لكل ملف) ===
                    if (!fastDone && !aborted && d.Kind == "mtp")
                    {
                        Log("  📟 نقل عبر MTP (نسخ + تحقق بالحجم)...");
                        bool mtpCancelled = MtpWriteCategory(d, r.dp, r.local, r.files,
                            (name, okf, bytes) =>
                            {
                                done++;
                                if (okf) { okCount++; cok++; cbytes += bytes; totalBytes += bytes; Log("  [" + done + "/" + total + "]  ✔ " + name + "  (" + FmtSize(bytes) + ")"); }
                                else { failCount++; cfail++; failed.Add(r.cat.Name + "  |  " + name); Log("  [" + done + "/" + total + "]  ✗ فشل/لم يكتمل: " + name); LogError("MTP/" + r.cat.Name, name); }
                                SetProgress((double)done / total * 100); xDone = done; xBytes = totalBytes; SetCurrent("⇄ " + name); PostXfer("⇄ " + name);
                            },
                            () => xferCancel);
                        if (mtpCancelled) aborted = true;
                        fastDone = true;   // تم التعامل مع القسم عبر MTP — لا نكرّر بالمسار ملف-ملف
                    }

                    // === المسار المضمون: ملف ملف (آيفون، أو عند عدم تأكيد الدفعة) ===
                    if (!fastDone && !aborted)
                    {
                        foreach (var f in r.files)
                        {
                            string rel = f.Substring(r.local.Length).TrimStart('\\', '/').Replace('\\', '/');
                            string name = Path.GetFileName(f);
                            long sz = 0; try { sz = new FileInfo(f).Length; } catch { }
                            done++;
                            SetCurrent("⇄ " + name + "   (" + done + "/" + total + ")");
                            bool ok; string err = "";
                            try
                            {
                                if (d.Kind == "android")
                                {
                                    string destRemote = r.dp + "/" + rel;
                                    int slash = destRemote.LastIndexOf('/');
                                    string destDir = slash > 0 ? destRemote.Substring(0, slash) : r.dp;
                                    if (madeDirs.Add(destDir)) Shell.Run("adb", "-s " + d.Id + " shell mkdir -p " + Shell.Q(destDir));
                                    var pr = Shell.Run("adb", "-s " + d.Id + " push " + Shell.Q(f) + " " + Shell.Q(destRemote));
                                    ok = pr.ok; err = pr.outp;
                                }
                                else
                                {
                                    var pr = Shell.Run("pymobiledevice3", "afc push " + Shell.Q(f) + " " + (r.dp + "/" + rel) + " --udid " + d.Id);
                                    ok = pr.ok; err = pr.outp;
                                }
                            }
                            catch (Exception ex) { ok = false; err = ex.Message; }

                            if (ok) { okCount++; cok++; cbytes += sz; totalBytes += sz; Log("  [" + done + "/" + total + "]  ✔ " + name + "  (" + FmtSize(sz) + ")"); }
                            else { failCount++; cfail++; failed.Add(r.cat.Name + "  |  " + name); Log("  [" + done + "/" + total + "]  ✗ فشل: " + name); LogError("نقل/" + r.cat.Name, name + " | " + Trim(err)); }
                            SetProgress((double)done / total * 100);
                            xDone = done; xBytes = totalBytes; PostXfer("⇄ " + name);

                            // كشف انقطاع الهاتف أو إلغاء المستخدم — نوقف بأمان
                            if (!ok && IsDisconnect(err)) { aborted = true; break; }
                            if (xferCancel) { aborted = true; break; }
                        }
                    }
                    catStats.Add((r.cat.Name, r.files.Length, cok, cfail, cbytes));
                }

                if (aborted)
                {
                    bool byUser = xferCancel;
                    string why = byUser ? "أُلغي النقل بواسطة المستخدم" : "توقّف النقل: انقطع اتصال الهاتف";
                    SetCurrent(byUser ? "⏹ أُلغي النقل" : "⛔ انقطع الاتصال بالهاتف");
                    Log(""); Log((byUser ? "⏹ " : "⛔ ") + why + ".");
                    Log("الملفات المنقولة قبل التوقّف تبقى على الجهاز؛ أعد النقل لإكمال الباقي.");
                    LogOp(why + " (" + okCount + " ملف)");
                    // تقرير نهائي حتى عند الإلغاء/الانقطاع
                    double asecs = (DateTime.UtcNow - t0).TotalSeconds;
                    failed.Insert(0, "⚠ " + why + " — العملية لم تكتمل");
                    string arep = SaveReportDetailed(s, d, total, okCount, failCount, totalBytes, asecs, catStats, failed);
                    xLastReport = arep;
                    PostXfer(byUser ? "⏹ أُلغي النقل" : "⛔ انقطع الاتصال");
                    PostWeb(JsonSerializer.Serialize(new { type = "xdone", status = (byUser ? "⏹ أُلغي النقل" : "⛔ انقطع الاتصال"), done = okCount, toast = why }));
                    return;
                }

                SetProgress(100); SetCurrent("✅ اكتمل النقل");
                double secs = (DateTime.UtcNow - t0).TotalSeconds;
                Log(""); Log("═══════ التقرير المفصّل ═══════");
                foreach (var cs in catStats) Log("• " + cs.name + ": " + cs.ok + "/" + cs.total + " ملف  (" + FmtSize(cs.bytes) + ")" + (cs.fail > 0 ? "  فشل " + cs.fail : ""));
                Log("────────────────────────");
                Log("الإجمالي: " + total + " ملف  |  نجح: " + okCount + "  |  فشل: " + failCount);
                Log("الحجم الكلي: " + FmtSize(totalBytes) + "  |  المدة: " + (int)secs + " ثانية" + (secs > 0 ? "  |  السرعة: " + FmtSize((long)(totalBytes / secs)) + "/ث" : ""));
                if (failCount > 0) { Log("الفاشلة:"); foreach (var x in failed) Log("   ✗ " + x); }
                string repPath = SaveReportDetailed(s, d, total, okCount, failCount, totalBytes, secs, catStats, failed);
                xLastReport = repPath;
                Log("✅ انتهى — التقرير المفصّل انفتح تلقائياً.");
                LogOp(string.Format("تم نقل {0} ملف بنجاح ({1})", okCount, FmtSize(totalBytes)));
                xDone = okCount; xBytes = totalBytes; PostXfer("✅ اكتمل النقل");
                PostWeb(JsonSerializer.Serialize(new { type = "xdone", status = "✅ اكتمل النقل", done = okCount, toast = "اكتمل النقل: نجح " + okCount + "، فشل " + failCount }));
                if (repPath != null) { try { System.Diagnostics.Process.Start(new ProcessStartInfo(repPath) { UseShellExecute = true }); } catch { } }
            }
            finally
            {
                EndOp();
                try { Directory.Delete(tmp, true); } catch { }
                if (btnStart.InvokeRequired) btnStart.BeginInvoke(new Action(() => { btnStart.Enabled = true; btnStart.Text = "▶  ابدأ النقل"; }));
            }
        }

        // تحقق كامل: يقارن حجم كل ملف على الوجهة (ADB) بالحجم المحلي — يرجع المسارات غير المطابقة/المفقودة
        HashSet<string> AdbVerifyAll(Device d, string destBase, string localBase, string[] files)
        {
            var bad = new HashSet<string>();
            var want = new Dictionary<string, long>();          // remotePath -> localSize
            var relByRemote = new Dictionary<string, string>(); // remotePath -> relPath
            foreach (var f in files)
            {
                string rel = f.Substring(localBase.Length).TrimStart('\\', '/').Replace('\\', '/');
                string remote = destBase + "/" + rel;
                long lsz = 0; try { lsz = new FileInfo(f).Length; } catch { }
                want[remote] = lsz; relByRemote[remote] = rel;
            }
            var got = new Dictionary<string, long>();            // remotePath -> deviceSize
            var keys = new List<string>(want.Keys);
            for (int i = 0; i < keys.Count; i += 100)
            {
                if (xferCancel) break;
                var chunk = keys.GetRange(i, Math.Min(100, keys.Count - i));
                // صيغة "المسار|الحجم" لكل ملف؛ علامات اقتباس مفردة تحفظ | على جهاز الأندرويد
                string args = "-s " + d.Id + " shell stat -c '%n|%s' " + string.Join(" ", chunk.Select(Shell.Q));
                var rr = Shell.Run("adb", args, 120000);
                if (IsDisconnect(rr.outp)) break;
                foreach (var ln in (rr.outp ?? "").Replace("\r", "").Split('\n'))
                {
                    var line = ln.Trim(); if (line.Length == 0) continue;
                    int bar = line.LastIndexOf('|'); if (bar <= 0) continue;
                    string rp = line.Substring(0, bar).Trim();
                    long sz; if (long.TryParse(line.Substring(bar + 1).Trim(), out sz)) got[rp] = sz;
                }
            }
            foreach (var kv in want)
            {
                long g;
                if (!got.TryGetValue(kv.Key, out g) || g != kv.Value) bad.Add(relByRemote[kv.Key]);
            }
            return bad;
        }

        // كشف انقطاع الهاتف من مخرجات adb
        static bool IsDisconnect(string outp)
        {
            if (string.IsNullOrEmpty(outp)) return false;
            string o = outp.ToLower();
            return o.Contains("device not found") || o.Contains("no devices") || o.Contains("device offline")
                || o.Contains("not found") && o.Contains("device") || o.Contains("protocol fault")
                || o.Contains("connection reset") || o.Contains("device still authorizing") || o.Contains("closed");
        }

        (bool ok, string outp) ReadFolder(Device dev, string remote, string local)
        {
            if (dev.Kind == "android") return Shell.Run("adb", "-s " + dev.Id + " pull " + remote + " " + Shell.Q(local), 3600000);
            if (dev.Kind == "mtp") return ReadFolderMtp(dev, remote, local);
            Directory.CreateDirectory(local);
            return Shell.Run("pymobiledevice3", "afc pull " + remote + " " + Shell.Q(local) + " --udid " + dev.Id, 3600000);
        }

        static string Trim(string s) { s = (s ?? "").Trim(); return s.Length > 140 ? s.Substring(0, 140) : s; }
    }
}
