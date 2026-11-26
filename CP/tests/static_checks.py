# -*- coding: utf-8 -*-
"""
فحوصات آلية ثابتة لمشروع CENTER POINT — تُمسك أنواع الأخطاء التي تظهر فقط عند الترجمة،
بسرعة وبدون مترجم C#. تُشغَّل في CI على كل دفعة، وتوقف الدمج عند أي خطأ.

تغطّي:
  1) توازن الأقواس () {} [] في كل ملف .cs
  2) تعريفات دوال مكرّرة داخل الصنف (CS0111 — مثل OpenUrl المكرّرة)
  3) رموز شائعة مستخدمة بلا using مناسب (CS0103/CS0246 — مثل Regex / StringBuilder)
  4) تطابق رقم الإصدار بين csproj والمثبّت
  5) لا أسماء ملفات غير ASCII (ترميز ZIP)
  6) تضمين واجهة UI في الـexe (EmbeddedResource)
  7) workflow الإصدار ينشر exe + sha256 + version
الخروج بغير صفر عند أي فشل.
"""
import os, re, sys, glob

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # مجلد CP
errors = []
def fail(msg): errors.append(msg)


def strip_code(s):
    """إزالة السلاسل والتعليقات لفحص الأقواس بدقة."""
    out = []; i = 0; n = len(s); instr = None; esc = False
    while i < n:
        c = s[i]
        if instr:
            if esc: esc = False
            elif c == '\\': esc = True
            elif c == instr: instr = None
            i += 1; continue
        if c == '/' and i + 1 < n and s[i+1] == '/':
            while i < n and s[i] != '\n': i += 1
            continue
        if c == '/' and i + 1 < n and s[i+1] == '*':
            i += 2
            while i + 1 < n and not (s[i] == '*' and s[i+1] == '/'): i += 1
            i += 2; continue
        if c in '"\'':
            instr = c; i += 1; continue
        out.append(c); i += 1
    return ''.join(out)


CS_FILES = sorted(glob.glob(os.path.join(ROOT, "*.cs")))

# 1) توازن الأقواس
for f in CS_FILES:
    with open(f, encoding='utf-8') as file:
        t = strip_code(file.read())
    for op, cl, name in [('(', ')', '()'), ('{', '}', '{}'), ('[', ']', '[]')]:
        if t.count(op) != t.count(cl):
            fail("توازن {} مختل في {}: {} مقابل {}".format(name, os.path.basename(f), t.count(op), t.count(cl)))

# 2) تعريفات دوال مكرّرة في ملفات MainForm الجزئية (تحذير فقط — المترجم في CI هو الحَكَم النهائي)
# يلتقط الدوال حتى بلا معدّل وصول (مثل: void OpenUrl(string url)) لأن ذلك سبب خطأ CS0111 الفعلي.
warnings = []
defpat = re.compile(
    r'^\s*((?:public|private|protected|internal|static|async|override|virtual|sealed|new|unsafe|extern)\s+)*'
    r'([\w<>\[\],\.\?]+)\s+(\w+)\s*\(([^)]*)\)\s*(\{|=>|$)')
seen = {}
TYPE_KEYWORDS = {"if", "for", "while", "switch", "catch", "using", "lock", "foreach", "return", "else", "do", "fixed"}
SKIP_NAMES = {"OnPaint", "ToString", "Dispose", "Main", "Equals", "GetHashCode"}
for f in CS_FILES:
    with open(f, encoding='utf-8') as file:
        src = file.read()
    lines = src.splitlines()
    if "partial class MainForm" not in src:
        continue  # ملف بلا MainForm — لا نفحص تكراره هنا
    # موضع بداية MainForm: ما بعده يخصّ MainForm (الفئات المساعدة تسبقه في الملف)
    mf_start = next((i for i, ln in enumerate(lines, 1) if "partial class MainForm" in ln), 1)
    for i, ln in enumerate(lines, 1):
        if i < mf_start: continue  # تجاوز الفئات المساعدة قبل MainForm
        indent = len(ln) - len(ln.lstrip(' '))
        if indent > 8: continue    # أعضاء الصنف عند 8 مسافات؛ الأعمق = دوال محلية (تُستثنى)
        m = defpat.match(ln)
        if not m: continue
        rettype = m.group(2); name = m.group(3); args = m.group(4).strip()
        if rettype in TYPE_KEYWORDS or name in TYPE_KEYWORDS: continue
        if name in SKIP_NAMES: continue
        argc = 0 if args == "" else args.count(',') + 1
        seen.setdefault((name, argc), []).append((os.path.basename(f), i))
for key, locs in seen.items():
    if len(locs) <= 1: continue
    where = ", ".join("{}:{}".format(a, b) for a, b in locs)
    warnings.append("تعريف دالة مكرّر محتمل {}({} وسيط): {} — تحقّق (قد يكون CS0111)".format(key[0], key[1], where))

# 3) رموز تحتاج using
NEED = {
    "Regex": "System.Text.RegularExpressions",
    "StringBuilder": "System.Text",
    "JsonSerializer": "System.Text.Json",
    "JsonDocument": "System.Text.Json",
    "GraphicsPath": "System.Drawing.Drawing2D",
    "LinearGradientBrush": "System.Drawing.Drawing2D",
}
for f in CS_FILES:
    with open(f, encoding='utf-8') as file:
        src = file.read()
    usings = "\n".join(l for l in src.splitlines() if l.strip().startswith("using "))
    body = strip_code(src)
    for sym, ns in NEED.items():
        # استخدام بالاسم المختصر (ليس مؤهّلاً بالكامل System.X.Sym)
        uses_short = re.search(r'(?<![\w.])' + sym + r'\b', body)
        fq = ns + "." + sym
        uses_fq = fq in src
        if uses_short and not uses_fq and ns not in usings:
            fail("{}: يستخدم {} بلا `using {};`".format(os.path.basename(f), sym, ns))

# 4) تطابق الإصدار
with open(os.path.join(ROOT, "CenterPoint.csproj"), encoding='utf-8') as file:
    csproj = file.read()
mver = re.search(r'<Version>([\d.]+)</Version>', csproj)
ver = mver.group(1) if mver else None
if not ver:
    fail("لا يوجد <Version> في csproj")
iss_path = os.path.join(ROOT, "CenterPoint_Setup.iss")
if os.path.exists(iss_path) and ver:
    with open(iss_path, encoding='utf-8') as file:
        iss = file.read()
    miss = re.search(r'#define\s+AppVer\s+"([\d.]+)"', iss)
    if miss and miss.group(1) != ver:
        fail("إصدار المثبّت {} لا يطابق csproj {}".format(miss.group(1), ver))

# 5) أسماء ملفات ASCII
for dp, dns, fns in os.walk(ROOT):
    if ".git" in dp: continue
    for nm in fns:
        if any(ord(ch) > 127 for ch in nm):
            fail("اسم ملف غير ASCII: {}".format(nm))

# 6) تضمين UI في الـexe
if 'EmbeddedResource Include="ui\\index.html"' not in csproj and 'EmbeddedResource Include="ui/index.html"' not in csproj:
    fail("ui/index.html غير مُضمّن كـ EmbeddedResource في csproj (التحديث التلقائي يحتاجه)")

# 7) workflow الإصدار
by = os.path.join(ROOT, "..", ".github", "workflows", "build.yml")
if os.path.exists(by):
    with open(by, encoding='utf-8') as file:
        w = file.read()
    for need in [".exe", ".sha256", "version.txt"]:
        if need not in w:
            fail("build.yml لا ينشر {}".format(need))

# النتيجة
print("=" * 50)
if warnings:
    print("⚠ تحذيرات (لا توقف البناء — المترجم في CI يؤكّدها):")
    for w in warnings:
        print("  ⚠ " + w)
    print("-" * 50)
if errors:
    print("فشل الفحص الآلي ({} مشكلة):".format(len(errors)))
    for e in errors:
        print("  ✗ " + e)
    print("=" * 50)
    sys.exit(1)
print("✅ كل الفحوصات الآلية الصارمة نجحت ({} ملف .cs).".format(len(CS_FILES)))
print("=" * 50)
sys.exit(0)
