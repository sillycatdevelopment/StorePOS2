@echo off
REM StorePOS 2.0 - busca e instala actualizaciones desde GitHub (no borra tus datos)
title Actualizar StorePOS - Si este titulo empieza con Seleccionar, presiona Esc
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Actualizar-StorePOS.ps1"
