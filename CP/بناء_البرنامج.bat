@echo off
chcp 65001 >nul
title Center Point - بناء تلقائي
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ============================================
echo    سنتر بوينت - بناء البرنامج (تلقائي)
echo ============================================
echo.

REM --- 1) البحث عن .NET ---
set "DOTNET=dotnet"
where dotnet >nul 2>&1 && goto build

if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
  set "DOTNET=%USERPROFILE%\.dotnet\dotnet.exe"
  goto build
)

REM --- 2) تنصيب .NET تلقائياً (يحتاج إنترنت - مرة واحدة) ---
echo .NET SDK غير موجود. جاري تنصيبه تلقائياً...
echo (هذي الخطوة تحتاج إنترنت وتصير مرة وحدة فقط)
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile \"$env:TEMP\dotnet-install.ps1\"; & \"$env:TEMP\dotnet-install.ps1\" -Channel 8.0 -InstallDir \"$env:USERPROFILE\.dotnet\""

if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
  set "DOTNET=%USERPROFILE%\.dotnet\dotnet.exe"
) else (
  echo.
  echo [خطأ] فشل تنصيب .NET. تأكد من الإنترنت وأعد المحاولة.
  pause
  exit /b 1
)

:build
echo.
echo جاري بناء البرنامج... انتظر لحظات
echo.
"%DOTNET%" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
  echo.
  echo [خطأ] فشل البناء. صوّر الرسالة أعلاه وابعثها.
  pause
  exit /b 1
)

REM --- 3) نسخ الملف النهائي بجنب هذا المجلد للتسهيل ---
set "OUT=bin\Release\net8.0-windows\win-x64\publish"
if exist "%OUT%\CenterPoint-Transfer.exe" (
  copy /y "%OUT%\CenterPoint-Transfer.exe" "CenterPoint-Transfer.exe" >nul
  copy /y "logo.png" "logo.png" >nul 2>&1
)

echo.
echo ============================================
echo   تم بنجاح!
echo   الملف جاهز: CenterPoint-Transfer.exe
echo   (انسخه مع logo.png لاي كمبيوتر وشغّله)
echo ============================================
echo.
pause
