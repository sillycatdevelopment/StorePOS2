@echo off
REM StorePOS 2.0 - busca actualizaciones, inicia el servidor y abre el navegador
cd /d "%~dp0"
if exist "%~dp0Actualizar-StorePOS.ps1" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Actualizar-StorePOS.ps1" -Auto
echo Iniciando StorePOS 2.0... (no cierres esta ventana mientras uses el sistema)
start "" "http://localhost:5000"
StorePos.Web.exe
pause
