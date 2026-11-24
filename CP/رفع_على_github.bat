@echo off
chcp 65001 >nul
title رفع مشروع CENTER POINT على GitHub
echo ============================================
echo   رفع مشروع CENTER POINT الى GitHub
echo ============================================
echo.

where git >nul 2>&1
if errorlevel 1 (
  echo [خطأ] Git غير مثبّت. حمّله من: https://git-scm.com/download/win
  pause & exit /b 1
)

if not exist ".git" (
  echo [1/5] تهيئة المستودع...
  git init
  git branch -M main
) else (
  echo [1/5] المستودع موجود.
)

echo [2/5] اضافة الملفات...
git add .

echo [3/5] حفظ نسخة (commit)...
git commit -m "CENTER POINT v8.1.0" 2>nul
if errorlevel 1 echo    (لا جديد للحفظ او تم مسبقا)

echo.
set /p REPO=الصق رابط المستودع (مثال https://github.com/USERNAME/center-point.git): 
if "%REPO%"=="" (
  echo [تجاوز] لم يُدخل رابط — تم الحفظ محلياً فقط.
  pause & exit /b 0
)

echo [4/5] ربط المستودع البعيد...
git remote remove origin 2>nul
git remote add origin %REPO%

echo [5/5] الرفع...
git push -u origin main
if errorlevel 1 (
  echo.
  echo [تنبيه] فشل الرفع. تأكد ان:
  echo   - المستودع موجود على GitHub ^(انشئه فارغاً اولاً^)
  echo   - سجّلت الدخول ^(سيطلب المتصفح/التوكن اول مرة^)
) else (
  echo.
  echo ✔ تم الرفع بنجاح.
  echo لنشر اصدار exe تلقائياً:  git tag v8.1.0  ثم  git push origin v8.1.0
)
echo.
pause
