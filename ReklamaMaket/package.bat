@echo off
rem Reklama Maket: saytga qo'yiladigan tayyor paket (sotuvchi uchun)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0package.ps1" %*
pause
