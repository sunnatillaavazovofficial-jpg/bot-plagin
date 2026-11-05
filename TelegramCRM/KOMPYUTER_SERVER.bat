@echo off
chcp 65001 >nul
title TelegramCRM - kompyuterni server qilish
echo.
echo  ===========================================================
echo   TelegramCRM - shu kompyuterni server qilish
echo  ===========================================================
echo.
echo  Bu oyna yopilmaguncha kuting. Savol chiqsa - javob yozib Enter bosing.
echo.

set "PS1=%TEMP%\telegramcrm-server.ps1"
curl.exe -fsSL -o "%PS1%" "https://raw.githubusercontent.com/sunnatillaavazovofficial-jpg/bot-plagin/refs/heads/claude/blissful-brown-asw2bf/TelegramCRM/pc/server.ps1?t=%RANDOM%"
if not errorlevel 1 goto :run
if exist "%~dp0pc\server.ps1" (
  copy /y "%~dp0pc\server.ps1" "%PS1%" >nul
  goto :run
)
echo  Internetga ulanib bo'lmadi. Internetni tekshirib, qayta urinib ko'ring.
goto :xato

:run
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
if errorlevel 1 goto :xato
echo.
pause
exit /b 0

:xato
echo.
echo  XATO bo'ldi. Shu oynani rasmga oling (Win + Shift + S) va yuboring.
echo.
pause
exit /b 1
