# CENTER POINT

مشروع CENTER POINT لإدارة خدمات الهواتف والنقل والصيانة.

## النسخة الحالية
- Source baseline: `CenterPoint_V8.1_FULL` محفوظة خارج هذا المستودع كنسخة مرجعية.
- Project package: `CP/` ويحتوي مشروع Windows Forms على .NET 8.
- Version metadata: `12.0.0` (تهيئة V9، وليست إعلاناً عن إصدار نهائي مختبر).

## المتطلبات
- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 أو `dotnet` CLI
- WebView2 Runtime
- Inno Setup 6 لبناء ملف Setup عند الحاجة

## البناء
من مجلد المشروع:

```powershell
cd CP
dotnet restore .\CenterPoint.csproj
dotnet build .\CenterPoint.csproj -c Release
```

## الفحوصات الساكنة
من جذر المستودع:

```powershell
python CP/tests/verify_release.py
```

هذه الفحوصات لا تستبدل بناء Windows أو اختبارات الهواتف الحقيقية.

## سياسة التطوير
- عدم حذف وظائف V8.1 دون موافقة صريحة.
- التطوير على فروع مستقلة ودمج التغييرات بعد المراجعة.
- عدم تضمين كلمات مرور أو مفاتيح API أو بيانات عملاء حقيقية في المستودع.
- لا يُعلن دعم موديل هاتف أو نجاح Firmware إلا بعد اختبار موثق.

## خارطة الطريق
راجع `CP/docs/ROADMAP_V9_V12_AR.md`.
