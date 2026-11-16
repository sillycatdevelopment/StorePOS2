@echo off
REM StorePOS 2.0 - busca e instala actualizaciones desde GitHub (no borra tus datos)
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Actualizar-StorePOS.ps1"
