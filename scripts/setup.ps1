# StorePOS 2.0 — build, test and publish (run once on a PC with the .NET 10 SDK).
# Usage (PowerShell, in the repository folder):   .\scripts\setup.ps1
$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Host "== Restaurando herramientas y paquetes ==" -ForegroundColor Cyan
dotnet tool restore
dotnet restore StorePos2.slnx

if (-not (Test-Path "src/StorePos.Core/Migrations")) {
    Write-Host "== Creando la migración inicial de la base de datos ==" -ForegroundColor Cyan
    dotnet ef migrations add InitialCreate --project src/StorePos.Core --startup-project src/StorePos.Web --output-dir Migrations
}

Write-Host "== Pruebas automáticas ==" -ForegroundColor Cyan
dotnet test StorePos2.slnx -c Release

Write-Host "== Publicando para Windows x64 (no requiere instalar .NET en la tienda) ==" -ForegroundColor Cyan
dotnet publish src/StorePos.Web -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
Copy-Item scripts/Iniciar-StorePOS.bat, scripts/Actualizar-StorePOS.bat, scripts/Actualizar-StorePOS.ps1 publish/ -Force

Write-Host ""
Write-Host "Listo. Copia la carpeta 'publish' a la computadora de la tienda y abre Iniciar-StorePOS.bat" -ForegroundColor Green
