@echo off
rem Reklama Maket: litsenziya kaliti generatori (FAQAT SOTUVCHI UCHUN, xaridorga bermang)
cd /d "%~dp0"
dotnet run --project "%~dp0tools\KeyGen" -c Release -- %*
pause
