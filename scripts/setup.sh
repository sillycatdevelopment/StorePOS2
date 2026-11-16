#!/usr/bin/env bash
# StorePOS 2.0 — build, test and publish from macOS/Linux (produces a Windows build).
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet tool restore
dotnet restore StorePos2.slnx
if [ ! -d src/StorePos.Core/Migrations ]; then
  dotnet ef migrations add InitialCreate --project src/StorePos.Core --startup-project src/StorePos.Web --output-dir Migrations
fi
dotnet test StorePos2.slnx -c Release
dotnet publish src/StorePos.Web -c Release -r win-x64 --self-contained true -o publish
cp scripts/Iniciar-StorePOS.bat scripts/Actualizar-StorePOS.bat scripts/Actualizar-StorePOS.ps1 publish/
echo "Listo: carpeta publish/ (para Windows). Para probar aquí: dotnet run --project src/StorePos.Web"
