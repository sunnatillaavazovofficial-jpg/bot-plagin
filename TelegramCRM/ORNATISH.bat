@echo off
chcp 65001 >nul
title TelegramCRM - serverga o'rnatish
echo.
echo  ===========================================================
echo   TelegramCRM - serverga o'rnatish
echo  ===========================================================
echo.

rem --- To'ldirilgan .env faylini topamiz (avval .env, keyin .env.txt, keyin .env.example)
set "F="
for %%N in (.env .env.txt .env.example) do (
  if not defined F if exist "%USERPROFILE%\Desktop\TelegramCRM\%%N" set "F=%USERPROFILE%\Desktop\TelegramCRM\%%N"
  if not defined F if exist "%~dp0%%N" set "F=%~dp0%%N"
)
if not defined F (
  echo  .env fayli topilmadi.
  echo  Uni Desktop\TelegramCRM papkasiga yoki shu fayl turgan papkaga qo'ying.
  echo.
  pause
  exit /b 1
)
echo  Sozlamalar fayli: %F%
echo.

set "SERVER="
set /p SERVER= Server IP manzilini yozing va Enter bosing (masalan 81.85.51.22): 
if "%SERVER%"=="" (
  echo  IP manzil yozilmadi.
  pause
  exit /b 1
)

echo.
echo  1/2  Fayl serverga yuborilmoqda.
echo       Savol chiqsa: yes deb yozing. Parol so'rasa: server parolini yozing
echo       (yozganingiz ekranda ko'rinmaydi - bu normal).
echo.
scp "%F%" root@%SERVER%:/root/crm.env
if errorlevel 1 goto :xato

echo.
echo  2/2  Dastur o'rnatilmoqda (5-8 daqiqa). Parolni yana bir marta so'raydi.
echo.
ssh -t root@%SERVER% "curl -fsSL https://raw.githubusercontent.com/sunnatillaavazovofficial-jpg/bot-plagin/refs/heads/claude/blissful-brown-asw2bf/TelegramCRM/install.sh -o install.sh && ENV_FILE=/root/crm.env bash install.sh"
if errorlevel 1 goto :xato

echo.
echo  ===========================================================
echo   TAYYOR! Yuqorida yozilgan sayt manzilini brauzerda oching.
echo  ===========================================================
echo.
pause
exit /b 0

:xato
echo.
echo  XATO bo'ldi. Shu oynani rasmga oling (Win + Shift + S) va yuboring.
echo.
pause
exit /b 1
