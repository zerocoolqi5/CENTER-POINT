@echo off
chcp 65001 >nul
title Center Point - بناء وتثبيت
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ============================================
echo    سنتر بوينت - بناء وتثبيت البرنامج
echo ============================================
echo.
echo [مرحلة 1/5] فحص المتطلبات...
echo.

REM --- 1) البحث عن .NET ---
set "DOTNET=dotnet"
where dotnet >nul 2>&1 && goto build
if exist "%USERPROFILE%\.dotnet\dotnet.exe" ( set "DOTNET=%USERPROFILE%\.dotnet\dotnet.exe" & goto build )

echo .NET SDK غير موجود. جاري تنصيبه تلقائياً... (يحتاج إنترنت مرة وحدة)
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile \"$env:TEMP\dotnet-install.ps1\"; & \"$env:TEMP\dotnet-install.ps1\" -Channel 8.0 -InstallDir \"$env:USERPROFILE\.dotnet\""
if exist "%USERPROFILE%\.dotnet\dotnet.exe" ( set "DOTNET=%USERPROFILE%\.dotnet\dotnet.exe" ) else ( echo [خطأ] فشل تنصيب .NET. & pause & exit /b 1 )

:build
echo [مرحلة 2/5] بناء البرنامج... انتظر لحظات
echo.
"%DOTNET%" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 ( echo. & echo [خطأ] فشل البناء. صوّر الرسالة وابعثها. & pause & exit /b 1 )

set "OUT=bin\Release\net8.0-windows\win-x64\publish"
if not exist "%OUT%\CenterPoint-Transfer.exe" ( echo [خطأ] لم يتم العثور على الملف المبني. & pause & exit /b 1 )

REM --- 2) التثبيت: نسخ لمجلد ثابت + اختصارات ---
echo.
echo [مرحلة 3/5] التثبيت على الويندوز...
set "INSTDIR=%LOCALAPPDATA%\CenterPoint Transfer"
if not exist "%INSTDIR%" mkdir "%INSTDIR%"
copy /y "%OUT%\CenterPoint-Transfer.exe" "%INSTDIR%\CenterPoint-Transfer.exe" >nul
if exist "logo.png" copy /y "logo.png" "%INSTDIR%\logo.png" >nul
REM نسخ ملفات الواجهة الاحترافية (WebView2)
if exist "%OUT%\ui" xcopy /e /i /y "%OUT%\ui" "%INSTDIR%\ui" >nul
REM نسخ سكربت إزالة البرنامج بجانب التثبيت
if exist "uninstall.bat" copy /y "uninstall.bat" "%INSTDIR%\uninstall.bat" >nul

REM اختصار على سطح المكتب وقائمة Start
powershell -NoProfile -Command ^
  "$exe='%INSTDIR%\CenterPoint-Transfer.exe';" ^
  "$W=New-Object -ComObject WScript.Shell;" ^
  "$d=$W.CreateShortcut([Environment]::GetFolderPath('Desktop')+'\CENTER POINT TRANSFER.lnk');$d.TargetPath=$exe;$d.WorkingDirectory='%INSTDIR%';$d.IconLocation=$exe;$d.Save();" ^
  "$sm=[Environment]::GetFolderPath('StartMenu')+'\Programs';" ^
  "$s=$W.CreateShortcut($sm+'\CENTER POINT TRANSFER.lnk');$s.TargetPath=$exe;$s.WorkingDirectory='%INSTDIR%';$s.IconLocation=$exe;$s.Save();"

REM --- 4) فحص ADB (platform-tools) لنقل ADB الكامل ---
echo.
echo [مرحلة 4/5] فحص ADB (أدوات أندرويد)...
set "ADBOK=0"
where adb >nul 2>&1 && set "ADBOK=1"
if exist "%USERPROFILE%\platform-tools\adb.exe" set "ADBOK=1"
if exist "%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe" set "ADBOK=1"
if "%ADBOK%"=="1" (
  echo [تحقق] ADB متوفر.
) else (
  echo [تنبيه] ADB غير موجود - جاري التحميل تلقائياً... (يحتاج إنترنت)
  powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='SilentlyContinue'; try{ $u='https://dl.google.com/android/repository/platform-tools-latest-windows.zip'; $o=Join-Path $env:TEMP 'platform-tools.zip'; Invoke-WebRequest -Uri $u -OutFile $o -UseBasicParsing; $tmp=Join-Path $env:TEMP 'cp_pt'; if(Test-Path $tmp){Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue}; Expand-Archive -Path $o -DestinationPath $tmp -Force; $dst=Join-Path $env:USERPROFILE 'platform-tools'; if(!(Test-Path $dst)){New-Item -ItemType Directory -Path $dst | Out-Null}; Copy-Item (Join-Path $tmp 'platform-tools\*') $dst -Recurse -Force -ErrorAction SilentlyContinue; Write-Host 'تم تجهيز ADB.' }catch{ Write-Host 'تعذّر تحميل ADB تلقائياً. نقل MTP يعمل بدونه.' }" 2>nul
)

REM --- 5) فحص WebView2 Runtime (مطلوب للواجهة الفاخرة) ---
echo.
echo [مرحلة 5/5] فحص WebView2 Runtime...
set "WV2=0"
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV2=1"
reg query "HKCU\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV2=1"
if "%WV2%"=="0" (
  echo.
  echo [تنبيه] WebView2 Runtime غير مثبّت - جاري التحميل والتثبيت تلقائياً...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "try{ $u='https://go.microsoft.com/fwlink/p/?LinkId=2124703'; $o=Join-Path $env:TEMP 'MicrosoftEdgeWebview2Setup.exe'; Invoke-WebRequest -Uri $u -OutFile $o -UseBasicParsing; Start-Process -Wait -FilePath $o -ArgumentList '/silent','/install'; Write-Host 'تم تثبيت WebView2 Runtime.' }catch{ Write-Host 'تعذّر التثبيت التلقائي - سنفتح صفحة التحميل اليدوي.'; Start-Process 'https://developer.microsoft.com/microsoft-edge/webview2/' }"
  echo.
) else (
  echo [تحقق] WebView2 Runtime متوفر.
)

echo.
echo ============================================
echo   تم التثبيت بنجاح!
echo   البرنامج: %INSTDIR%
echo   اختصار على سطح المكتب وقائمة Start باسم:
echo   CENTER POINT TRANSFER
echo ============================================
echo.
echo هل تريد فتح البرنامج الآن؟ اضغط أي زر...
pause >nul
start "" "%INSTDIR%\CenterPoint-Transfer.exe"
exit /b 0
