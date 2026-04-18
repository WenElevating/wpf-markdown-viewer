@echo off
chcp 65001 >nul
setlocal

echo ============================================
echo   WPF Markdown Editor - Build Installer
echo ============================================
echo.

:: Step 1: Publish
echo [1/2] Publishing Release build...
dotnet publish samples\WpfMarkdownEditor.Sample -c Release -r win-x64 --self-contained false -o samples\WpfMarkdownEditor.Sample\bin\Release\net8.0-windows\publish -v q
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Publish failed.
    pause
    exit /b 1
)
echo       Done.

:: Step 2: Build installer
echo.
echo [2/2] Building installer with Inno Setup...
"D:\Inno Setup 6\ISCC.exe" setup.iss
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Inno Setup compilation failed.
    pause
    exit /b 1

)

echo.
echo ============================================
echo   Done! Installer saved to:
echo   %CD%\installer-output\
echo ============================================
pause
