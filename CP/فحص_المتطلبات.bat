@echo off
chcp 65001 >nul
title Center Point - فحص المتطلبات
setlocal enabledelayedexpansion
color 0B

echo ============================================
echo    سنتر بوينت - فحص متطلبات التشغيل
echo ============================================
echo.

set "OKALL=1"

REM --- 1) .NET 8 (SDK أو Desktop Runtime) ---
set "NETOK=0"
where dotnet >nul 2>&1 && (
  for /f "delims=" %%v in ('dotnet --list-runtimes 2^>nul ^| findstr /i "Microsoft.WindowsDesktop.App 8."') do set "NETOK=1"
  for /f "delims=" %%v in ('dotnet --version 2^>nul') do set "SDKVER=%%v"
)
if exist "%USERPROFILE%\.dotnet\dotnet.exe" set "NETOK=1"
if "!NETOK!"=="1" ( echo [✔] .NET 8 متوفر   SDK=!SDKVER! ) else ( echo [✘] .NET 8 غير موجود  ^(مطلوب لبناء/تشغيل البرنامج^) & set "OKALL=0" )

REM --- 2) WebView2 Runtime (للواجهة) ---
set "WV2=0"
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV2=1"
reg query "HKCU\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV2=1"
reg query "HKLM\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV2=1"
if "!WV2!"=="1" ( echo [✔] WebView2 Runtime متوفر ) else ( echo [✘] WebView2 Runtime غير مثبّت  ^(يُثبّت تلقائياً عند التثبيت^) & set "OKALL=0" )

REM --- 3) ADB (platform-tools) لنقل ADB الكامل ---
set "ADBOK=0"
where adb >nul 2>&1 && set "ADBOK=1"
if exist "%USERPROFILE%\platform-tools\adb.exe" set "ADBOK=1"
if exist "%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe" set "ADBOK=1"
if "!ADBOK!"=="1" ( echo [✔] ADB متوفر  ^(نقل ADB الكامل مدعوم^) ) else ( echo [!] ADB غير موجود  ^(نقل MTP يعمل بدونه؛ ADB للنقل الكامل^) )

REM --- 4) إصدار ويندوز ---
for /f "tokens=4-5 delims=. " %%i in ('ver') do set WINVER=%%i.%%j
echo [i] نظام ويندوز: %WINVER%

echo.
echo ============================================
if "!OKALL!"=="1" (
  color 0A
  echo   النتيجة: كل المتطلبات الأساسية متوفرة ✔
) else (
  color 0E
  echo   النتيجة: ناقص بعض المتطلبات - شغّل «بناء_وتثبيت.bat»
  echo   وهو يثبّت الناقص تلقائياً ^(يحتاج إنترنت مرة واحدة^).
)
echo ============================================
echo.
pause >nul
exit /b 0
