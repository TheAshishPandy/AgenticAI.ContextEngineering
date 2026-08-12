@echo off
echo ============================================================
   Cleaning Duplicate DocumentUploaderHelper Files
============================================================
echo.

cd SmartChatBot.ContextEngineering

echo [1/3] Finding and deleting duplicate files...

:: Delete duplicate DocumentUploaderHelper files
if exist "SmartChatBot.ContextEngineering.Core\Core\Helper\DocumentUploaderHelper\DocumentUploaderHelper.cs" del "SmartChatBot.ContextEngineering.Core\Core\Helper\DocumentUploaderHelper\DocumentUploaderHelper.cs"
if exist "SmartChatBot.ContextEngineering.Core\Helper\DocumentUploaderHelper.cs" del "SmartChatBot.ContextEngineering.Core\Helper\DocumentUploaderHelper.cs"
if exist "SmartChatBot.ContextEngineering.Core\Core\Helper\DocumentUploaderHelper.cs" del "SmartChatBot.ContextEngineering.Core\Core\Helper\DocumentUploaderHelper.cs"

echo ✓ Duplicates deleted

echo.
echo [2/3] Cleaning bin/obj folders...
rmdir /s /q SmartChatBot.ContextEngineering.Core\bin 2>nul
rmdir /s /q SmartChatBot.ContextEngineering.Core\obj 2>nul
echo ✓ Cleaned

echo.
echo [3/3] Building...
dotnet build --configuration Debug

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo.
echo ============================================================
   Complete! Build successful.
============================================================
pause