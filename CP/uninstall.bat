@echo off
chcp 65001 >nul
title إزالة CENTER POINT TRANSFER
color 0C
echo ============================================
echo      إزالة برنامج CENTER POINT TRANSFER
echo ============================================
echo.

set "INSTDIR=%LOCALAPPDATA%\CenterPoint Transfer"
set "DESKTOP=%USERPROFILE%\Desktop\Center Point Transfer.lnk"
set "SM=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Center Point Transfer.lnk"

echo سيتم حذف:
echo   - مجلد البرنامج: %INSTDIR%
echo   - اختصار سطح المكتب
echo   - اختصار قائمة ابدأ
echo.
set /p OK="هل تريد المتابعة؟ (y/n): "
if /i not "%OK%"=="y" (
  echo تم الإلغاء.
  pause
  exit /b
)

echo.
echo جاري الإزالة...

REM إغلاق البرنامج إن كان يعمل
taskkill /f /im "CenterPoint-Transfer.exe" >nul 2>&1

REM حذف الاختصارات
if exist "%DESKTOP%" del /f /q "%DESKTOP%" >nul 2>&1
if exist "%SM%" del /f /q "%SM%" >nul 2>&1

REM حذف مجلد التثبيت
if exist "%INSTDIR%" (
  rmdir /s /q "%INSTDIR%"
  if exist "%INSTDIR%" (
    echo تعذّر حذف بعض الملفات - قد يكون البرنامج ما زال يعمل. أغلقه وأعد المحاولة.
  ) else (
    echo تم حذف مجلد البرنامج بنجاح.
  )
) else (
  echo مجلد البرنامج غير موجود ^(ربما أُزيل مسبقاً^).
)

echo.
echo ملاحظة: تقاريرك محفوظة في Documents\CenterPoint Transfer
echo          ولم تُحذف. ونسخك الاحتياطية في المجلد الذي اخترته تبقى سليمة.
echo.
echo ============================================
echo            تمت الإزالة.
echo ============================================
pause
