@echo off
chcp 65001 >nul
setlocal

:: Default to x64 when no architecture argument is supplied. arm64 also works
:: but requires Inno Setup 6.3+ (6.0.2 rejects ArchitecturesAllowed=arm64).
set "ARCH=%~1"
if "%ARCH%"=="" set "ARCH=x64"

set "PUBLISH_DIR=samples\WpfMarkdownEditor.Sample\bin\Release\net8.0-windows\publish\win-%ARCH%"
set "ISCC=D:\Inno Setup 6\ISCC.exe"

echo ============================================
echo   Quillora - Build Installer (%ARCH%)
echo ============================================
echo.

:: Step 1: Publish a self-contained build for the target architecture.
echo [1/2] Publishing self-contained %ARCH% build...
dotnet publish samples\WpfMarkdownEditor.Sample -c Release -r win-%ARCH% --self-contained true -o "%PUBLISH_DIR%" -v q
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Publish failed.
    pause
    exit /b 1
)
echo       Done.

:: Step 2: Build the installer, passing the architecture as an ISPP define so
:: setup.iss picks the right publish dir, output filename and arch restriction.
echo.
echo [2/2] Building installer with Inno Setup...
"%ISCC%" /DArch=%ARCH% setup.iss
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
