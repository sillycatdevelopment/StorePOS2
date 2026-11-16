# StorePOS 2.0 — actualizador.
# Busca la versión más reciente en GitHub Releases y, si es nueva, la instala SIN tocar la carpeta "data"
# (base de datos y respaldos) ni appsettings.json. Antes de actualizar copia la base de datos a
# "respaldo_actualizacion\<fecha>". Las migraciones de la base de datos se aplican solas al iniciar.
#
# Uso:  doble clic en Actualizar-StorePOS.bat     (pregunta antes de actualizar)
#       Actualizar-StorePOS.ps1 -Auto              (lo usa Iniciar-StorePOS.bat: actualiza sin preguntar)
param([switch]$Auto)

# ---- Configuración ----
$Repo  = "sillycatdevelopment/StorePOS2"   # usuario/repositorio en GitHub
$Asset = "StorePOS-Windows.zip"            # nombre del zip adjunto a cada Release
$Token = ""                                # solo si el repositorio es PRIVADO: token de GitHub con permiso de lectura

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Finish([int]$code) { if (-not $Auto) { Read-Host "Presiona Enter para salir" | Out-Null }; exit $code }

$versionFile = Join-Path $here "version.txt"
$current = if (Test-Path $versionFile) { (Get-Content $versionFile -Raw).Trim() } else { "" }

$headers = @{ "User-Agent" = "StorePOS-Updater"; "Accept" = "application/vnd.github+json" }
if ($Token) { $headers["Authorization"] = "Bearer $Token" }

Write-Host "Buscando actualizaciones de StorePOS..."
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers $headers -TimeoutSec 15
} catch {
    Write-Host "No se pudo consultar GitHub (¿sin internet?). Se usará la versión instalada." -ForegroundColor Yellow
    Finish 0
}

$latest = "$($release.tag_name)".Trim()
if ($latest -eq $current) {
    Write-Host "StorePOS ya está actualizado ($current)." -ForegroundColor Green
    Finish 0
}

$package = $release.assets | Where-Object { $_.name -eq $Asset } | Select-Object -First 1
if (-not $package) {
    Write-Host "La versión $latest no tiene el archivo $Asset adjunto. No se actualizó." -ForegroundColor Yellow
    Finish 0
}

Write-Host ("Nueva versión disponible: {0}  (instalada: {1})" -f $latest, $(if ($current) { $current } else { "desconocida" })) -ForegroundColor Cyan
if (-not $Auto) {
    $answer = Read-Host "¿Actualizar ahora? StorePOS se cerrará unos segundos (S/N)"
    if ($answer -notmatch '^[sS]') { Finish 0 }
}

$tmp = Join-Path $env:TEMP ("storepos_update_" + [guid]::NewGuid().ToString("N"))
try {
    # 1. Descargar y revisar el paquete ANTES de cerrar el sistema
    New-Item -ItemType Directory -Path $tmp | Out-Null
    $zip = Join-Path $tmp $Asset
    $dl = @{ "User-Agent" = "StorePOS-Updater"; "Accept" = "application/octet-stream" }
    if ($Token) { $dl["Authorization"] = "Bearer $Token"; $url = $package.url } else { $url = $package.browser_download_url }
    Write-Host "Descargando $latest..."
    Invoke-WebRequest -Uri $url -Headers $dl -OutFile $zip -UseBasicParsing
    $new = Join-Path $tmp "nuevo"
    Expand-Archive -Path $zip -DestinationPath $new -Force
    if (-not (Test-Path (Join-Path $new "StorePos.Web.exe"))) { throw "El paquete descargado no contiene StorePos.Web.exe." }

    # 2. Cerrar StorePOS si está abierto
    $running = Get-Process -Name "StorePos.Web" -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Cerrando StorePOS..."
        $running | Stop-Process -Force
        Start-Sleep -Seconds 2
    }

    # 3. Copia de seguridad de la base de datos
    $data = Join-Path $here "data"
    if (Test-Path (Join-Path $data "storepos.db")) {
        $backup = Join-Path $here ("respaldo_actualizacion\" + (Get-Date -Format "yyyyMMdd_HHmmss") + "_antes_de_" + $latest)
        New-Item -ItemType Directory -Force -Path $backup | Out-Null
        Get-ChildItem -Path $data -Filter "storepos.db*" -File | Copy-Item -Destination $backup -Force
        Write-Host "Respaldo de la base de datos: $backup"
    }

    # 4. Copiar archivos nuevos sin tocar data\, respaldos ni la configuración local
    $exclude = @("version.txt")
    if (Test-Path (Join-Path $here "appsettings.json")) { $exclude += "appsettings.json" }
    $rcArgs = @($new, $here, "/E", "/XD", "data", "respaldo_actualizacion", "/XF") + $exclude + @("/R:3", "/W:2", "/NFL", "/NDL", "/NJH", "/NJS", "/NP")
    & robocopy @rcArgs | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "No se pudieron copiar los archivos (robocopy $LASTEXITCODE)." }

    Set-Content -Path $versionFile -Value $latest -Encoding ASCII
    Write-Host "StorePOS actualizado a $latest. Tus datos no se modificaron." -ForegroundColor Green
}
catch {
    Write-Host "La actualización falló: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Tus datos están intactos. Puedes seguir usando la versión instalada." -ForegroundColor Yellow
}
finally {
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}
Finish 0
