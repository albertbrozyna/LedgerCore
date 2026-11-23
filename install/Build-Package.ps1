<#
.SYNOPSIS
  Buduje gotową do instalacji paczkę LedgerCore (dist\LedgerCore) – interfejs + API + SQLite,
  wraz ze środowiskiem .NET (komputer biura nie potrzebuje niczego doinstalowywać).
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File install\Build-Package.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist\LedgerCore'

Write-Host '1/3 Budowanie interfejsu (React)...' -ForegroundColor Cyan
Push-Location (Join-Path $root 'src\ledger-core-client')
npm ci
if ($LASTEXITCODE) { throw 'npm ci nie powiodło się' }
npm run build
if ($LASTEXITCODE) { throw 'Budowanie interfejsu nie powiodło się' }
Pop-Location

Write-Host '2/3 Publikowanie aplikacji (.NET, win-x64, samodzielna)...' -ForegroundColor Cyan
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
dotnet publish (Join-Path $root 'src\LedgerCore.Api\LedgerCore.Api.csproj') -c Release -r win-x64 --self-contained true -o $dist
if ($LASTEXITCODE) { throw 'dotnet publish nie powiodło się' }
# Dane i pliki deweloperskie nie należą do paczki
Remove-Item (Join-Path $dist 'data') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $dist '*.pdb') -Force -ErrorAction SilentlyContinue

Write-Host '3/3 Kopiowanie skryptów instalacyjnych...' -ForegroundColor Cyan
Copy-Item (Join-Path $PSScriptRoot 'Install.ps1') $dist
Copy-Item (Join-Path $PSScriptRoot 'Install.cmd') $dist
Copy-Item (Join-Path $PSScriptRoot 'Start-LedgerCore.cmd') $dist
Copy-Item (Join-Path $PSScriptRoot 'INSTRUKCJA.txt') $dist

$zip = Join-Path $root 'dist\LedgerCore-win-x64.zip'
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$dist\*" -DestinationPath $zip
Write-Host "Gotowe: $zip" -ForegroundColor Green
Write-Host 'Skopiuj ZIP na komputer biura, rozpakuj i uruchom Install.cmd.'
