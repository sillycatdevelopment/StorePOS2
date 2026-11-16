@echo off
REM StorePOS 2.0 - busca actualizaciones, inicia el servidor y abre el navegador
title StorePOS - NO CERRAR - Si este titulo empieza con Seleccionar, presiona Esc
cd /d "%~dp0"
echo.
echo   Si esta ventana parece detenida y su titulo empieza con "Seleccionar",
echo   haz clic aqui y presiona la tecla Esc.
echo.
if exist "%~dp0Actualizar-StorePOS.ps1" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Actualizar-StorePOS.ps1" -Auto
echo Iniciando StorePOS 2.0...
start "" "http://localhost:5000"
StorePos.Web.exe
pause
