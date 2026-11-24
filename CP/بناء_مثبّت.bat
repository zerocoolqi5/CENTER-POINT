@echo off
chcp 65001 >nul
title Center Point - بناء المثبّت الاحترافي
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ============================================
echo   سنتر بوينت - بناء مثبّت احترافي (Setup.exe)
echo ============================================
echo.

REM --- 1) نشر البرنامج (publish) ---
set "DOTNET=dotnet"
where dotnet >nul 2>&1 || if exist "%USERPROFILE%\.dotnet\dotnet.exe" set "DOTNET=%USERPROFILE%\.dotnet\dotnet.exe"
echo [1/2] نشر البرنامج...
"%DOTNET%" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 ( echo [خطأ] فشل النشر. & pause & exit /b 1 )

REM --- 2) تصريف سكربت Inno Setup ---
echo.
echo [2/2] بناء المثبّت عبر Inno Setup...
set "ISCC="
for %%p in ("%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" "%ProgramFiles%\Inno Setup 6\ISCC.exe" "%ProgramFiles(x86)%\Inno Setup 5\ISCC.exe") do if exist %%p set "ISCC=%%~p"
if "%ISCC%"=="" (
  echo [تنبيه] Inno Setup غير مثبّت.
  echo حمّله من: https://jrsoftware.org/isdl.php  ثم أعد تشغيل هذا الملف.
  start "" "https://jrsoftware.org/isdl.php"
  pause & exit /b 1
)
"%ISCC%" "CenterPoint_Setup.iss"
if errorlevel 1 ( echo [خطأ] فشل بناء المثبّت. & pause & exit /b 1 )

echo.
echo ============================================
echo   تم! المثبّت جاهز في مجلد: Setup_Output
echo   وزّعه على أجهزة المحل كملف Setup.exe واحد.
echo ============================================
pause >nul
exit /b 0
