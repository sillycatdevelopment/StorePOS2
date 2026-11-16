@echo off
REM StorePOS 2.0 - inicia el servidor y abre el navegador
cd /d "%~dp0"
echo Iniciando StorePOS 2.0... (no cierres esta ventana mientras uses el sistema)
start "" "http://localhost:5000"
StorePos.Web.exe
pause
